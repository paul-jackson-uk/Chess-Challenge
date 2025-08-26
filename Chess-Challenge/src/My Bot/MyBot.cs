using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using ChessChallenge.API;
using System.Collections;
using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
public class MyBot : IChessBot
{
    private const int checkmateScore = 1000000;
    public static int evaluationCount = 0;
    private List<Move>  move_seq = new();
    private Dictionary<ulong,CacheEntry> evaluated_positions = new();
    private int[] pieceValues = { 0, 100, 300, 300, 500, 900, 20000 }; // none, Pawn, Knight, Bishop, Rook, Queen, King

    record CacheEntry(int depth, int score);

    private int GetEdgeDistance(int squareIndex)
    {
        int row = squareIndex / 8;
        int col = squareIndex % 8;

        // Calculate the distance to the nearest edge
        int distanceToEdge = Math.Min(Math.Min(row, 7 - row), Math.Min(col, 7 - col));
        return distanceToEdge;
    }
    private int Evaluate(Board board, bool isWhite)
    {
        //System.Console.WriteLine("Evaluating position... FEN " + board.GetFenString());
        evaluationCount++;
        bool isEndGame = false;

        PieceList[] pieceLists = board.GetAllPieceLists();

        // Get opponent attacking peice count
        int opponentAttackingPieceCount = 0;
        IEnumerable<PieceList> opponentPieceQuery =
            from piece_list in pieceLists
            where piece_list.IsWhitePieceList != isWhite
            where pieceValues[(int) piece_list.TypeOfPieceInList] > 250
            where pieceValues[(int) piece_list.TypeOfPieceInList] < 1000
            select piece_list;

        foreach (PieceList pl in opponentPieceQuery)
        {
            opponentAttackingPieceCount += pl.Count * pieceValues[(int)pl.TypeOfPieceInList];
        }

        isEndGame = opponentAttackingPieceCount < 900;
        
        // Simple evaluation function: count material balance
        int score = 0;
        foreach (PieceList pl in pieceLists)
        {
            // add up material value of pieces
            int value = 0; 
            for (int i = 0; i < pl.Count; i++)
            {
                // Add positional score for pieces
                switch (pl.TypeOfPieceInList)
                {
                    case PieceType.Pawn:
                        value = pieceValues[(int)PieceType.Pawn];
                        break;
                    case PieceType.Knight:
                        // Add positional score for pieces
                        value = pieceValues[(int)PieceType.Knight] + 8 * GetEdgeDistance(pl.GetPiece(i).Square.Index);
                        break;
                    case PieceType.Bishop:
                        value = pieceValues[(int)PieceType.Bishop] + GetEdgeDistance(pl.GetPiece(i).Square.Index);
                        break;
                    case PieceType.Rook:
                        value = pieceValues[(int)PieceType.Rook];
                        break;
                    case PieceType.Queen:
                        value = pieceValues[(int)PieceType.Queen];
                        if (isEndGame)
                        {
                            // In endgame, we want the queen to be more active
                            value += 20 * GetEdgeDistance(pl.GetPiece(i).Square.Index);
                        }
                        break;
                    case PieceType.King:
                        if (isEndGame)
                        {
                            // In endgame, we want the king to be more active
                            value += 30 * GetEdgeDistance(pl.GetPiece(i).Square.Index);
                        }
                        break;
                }

                // Add or subtract piece value based on colour
                if (pl.IsWhitePieceList != isWhite) value = -value;
                score += value;
            }
        }

        // Boost for having more legal moves
        int legal_moves_boost = 10 - board.GetLegalMoves().Length;
        if (board.IsWhiteToMove == isWhite) legal_moves_boost = -legal_moves_boost;
        score += legal_moves_boost;

        return score;
    }

	static IEnumerable<Move> GetSortedMoves(Board board, bool checksAndCapturesOnly)
    {
        Move[] moves = board.GetLegalMoves();
        List<Move> checkMoves = new();
        List<Move> captureMoves = new();
        List<Move> normalMoves = new();

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            if (board.IsInCheckmate())
            {
                board.UndoMove(move);
                yield return move; // If this move results in checkmate, return it immediately
                yield break;
            }
            else if (move.IsCapture)
            {
                captureMoves.Add(move);
            }
            else if (board.IsInCheck())
            {
                checkMoves.Add(move);
            }
            else if (!checksAndCapturesOnly)
            {
                normalMoves.Add(move);
            }
 
            board.UndoMove(move);
        }

        // Provide the capture moves first
        foreach (Move move in captureMoves) yield return move;

        // Then the check moves
        foreach (Move move in checkMoves) yield return move;

        // Finally the normal moves
        foreach (Move move in normalMoves) yield return move;
        }

    class move_score_t
    {
        public Move move;
        public int score;

        public move_score_t(Move m, int s) { move = m; score = s; }
        override public string ToString() { return $"{move} score {score}::"; }
    }


    private (int, Move) get_best_move(Board board, bool isWhite, int depth, int max_depth, int alpha, int beta, List<Move> move_seq, int millisecondsAllowedPerTurn, ref bool abort_search, Timer timer)
    {
        Move best_move_this_level = Move.NullMove;
        bool ourMove = isWhite == board.IsWhiteToMove;
        int best_score_this_level = ourMove ? int.MinValue : int.MaxValue;
        List<move_score_t> move_scores = new();

        depth++;
        bool checksAndCapturesOnly = depth > max_depth;
        var sortedMoves = GetSortedMoves(board, checksAndCapturesOnly);

        // Now start the analysis
        foreach (Move move in sortedMoves)
        {
            int score = 0;
            board.MakeMove(move);

            // Have already evaluated this position?
            if (evaluated_positions.TryGetValue(board.ZobristKey, out var cached_entry) && cached_entry.depth >= max_depth)
            {
                score = cached_entry.score;
            }
            else
            {
                if (board.IsDraw())
                {
                    score = 0;
                }
                else if (board.IsInCheckmate())
                {
                    score = ourMove ? checkmateScore : -checkmateScore;
                }
                else
                {
                    move_seq.Add(move);

                    (score, _) = get_best_move(board, isWhite, depth, max_depth, alpha, beta, move_seq, millisecondsAllowedPerTurn, ref abort_search, timer);
                    // if we are only looking at checks and captures then we should evaluate the current position and
                    // compare it with the scores from checks and captures
                    if (checksAndCapturesOnly)
                    {
                        int evalScore = Evaluate(board, isWhite);
                        score = ourMove ? Math.Max(score, evalScore) : Math.Min(score, evalScore);
                    }
                    move_seq.RemoveAt(move_seq.Count() - 1);
                }

                // Add the new position to the cache. Also replaces it if already present
                evaluated_positions[board.ZobristKey] = new CacheEntry(depth, score);
            }
            board.UndoMove(move);

            if (max_depth > 1 && millisecondsAllowedPerTurn < timer.MillisecondsElapsedThisTurn)
            {
                abort_search = true;
                return (0, Move.NullMove); // doesn't mattter what we return
            }

            if (ourMove)
            {
                // our move
                if (score > best_score_this_level)
                {
                    best_score_this_level = score;
                    best_move_this_level = move;
                    alpha = Math.Max(alpha, score);
                    if (beta <= alpha)
                    {
                        return (best_score_this_level, best_move_this_level);
                    }
                }
            }
            else
            {
                // opponent's move
                if (score < best_score_this_level)
                {
                    best_score_this_level = score;
                    best_move_this_level = move;
                    beta = Math.Min(beta, score);
                    if (beta <= alpha)
                    {
                        // Alpha cut-off
                        return (best_score_this_level, best_move_this_level);
                    }

                }
            }
        }

        if (best_move_this_level != Move.NullMove)
        {
            return (best_score_this_level, best_move_this_level);
        }
        else
        {
            // We must have looked at no moves because we are at max depth and there are no captures or checks
            return (Evaluate(board, isWhite), Move.NullMove);
        }
    }

    public Move Think(Board board, Timer timer)
    {
        System.Console.WriteLine($"Thinking... FEN: {board.GetFenString()}");
        evaluationCount = 0;
        evaluated_positions.Clear();

        int millisecondsAllowedPerTurn = 400;
        int max_depth = 8;

		Move best_move = Move.NullMove;
        int best_score = 0;
        int alpha = int.MinValue;
        int beta = int.MaxValue;

        int depth = 1;
        bool abort_search = false;

        while (depth < max_depth && timer.MillisecondsElapsedThisTurn < millisecondsAllowedPerTurn)
        {
            (int bs, Move bm) = get_best_move(board, board.IsWhiteToMove, 0, depth, alpha, beta, move_seq, millisecondsAllowedPerTurn, ref abort_search, timer);
            if (!abort_search)
            {
                best_move = bm;
                best_score = bs;
            }
            Console.WriteLine($"Depth: {depth}, Best Move: {best_move}, Score: {best_score}, Time taken: {timer.MillisecondsElapsedThisTurn}ms, Evaluations: {evaluationCount}\n");
            depth++;
        }

        return best_move;
    }
}