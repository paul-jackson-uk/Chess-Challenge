using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using ChessChallenge.API;
using System.Collections;
using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using Microsoft.CodeAnalysis;


public class MyBot : IChessBot
{
    private const int checkmateScore = 1000000;
    public static int evaluationCount = 0;
    private Dictionary<ulong, CacheEntry> evaluated_positions = new();
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

        int opponentAttackingPieceCount = 0;
        ulong oppAttackingPieceBB = (isWhite ? board.BlackPiecesBitboard : board.WhitePiecesBitboard) &
                    ~(board.GetPieceBitboard(PieceType.King, !isWhite) | board.GetPieceBitboard(PieceType.Pawn, !isWhite));
        opponentAttackingPieceCount = BitboardHelper.GetNumberOfSetBits(oppAttackingPieceBB);
        isEndGame = (opponentAttackingPieceCount < 4) && board.GetPieceList(PieceType.Queen, !isWhite).Count > 0;

        // Simple evaluation function: count material balance
        int score = 0;
        foreach (PieceList pl in pieceLists)
        {
            // add up material value of pieces
            int value = 0;
            for (int i = 0; i < pl.Count; i++)
            {
                // Add material value
                if (pl.TypeOfPieceInList is not PieceType.King) value = pieceValues[(int)pl.TypeOfPieceInList];

                // Add positional score for pieces
                switch (pl.TypeOfPieceInList)
                {
                    case PieceType.Pawn:
                        value += 20 * (GetEdgeDistance(pl.GetPiece(i).Square.Index) - 1);
                        break;
                    case PieceType.Knight:
                        // Add positional score for pieces
                        value += 8 * BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetKnightAttacks(pl.GetPiece(i).Square));
                break;
                    case PieceType.Bishop:
                case PieceType.Rook:
                    value += (4 * BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetSliderAttacks(pl.TypeOfPieceInList, pl.GetPiece(i).Square, board)));
                    break;
                case PieceType.Queen:
                    if (isEndGame)
                    {
                        // In endgame, we want the queen to be more active
                        value += 20 * GetEdgeDistance(pl.GetPiece(i).Square.Index);
                    }
                    else if (board.PlyCount < 10)
                    {
                        var queenRank = pl.GetPiece(i).Square.Rank;
                        if (!board.IsWhiteToMove) queenRank = 7 - queenRank;
                        value += 20 * (3 - queenRank);
                    }
                    break;
                case PieceType.King:
                    if (isEndGame)
                    {
                        // In endgame, we want the king to be more active
                        value += 30 * GetEdgeDistance(pl.GetPiece(i).Square.Index);
                    }
                    else
                    {
                        // Keep the king on the back rank ideally towards the corner
                        var kingSquare = board.GetKingSquare(board.IsWhiteToMove);
                        int kingRank = board.IsWhiteToMove ? kingSquare.Rank : 7 - kingSquare.Rank;
                        value += ((7 - kingRank) * 10) + (Math.Max(kingSquare.File, (7 - kingSquare.File)) << 3);
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


    IEnumerable<Move> GetSortedMoves(Board board, bool checksAndCapturesOnly)
    {
        Move[] moves = board.GetLegalMoves();
        List<Move> checkMoves = new();
        List<Move> bestCaptures = new();
        List<Move> equalCaptureMoves = new();
        List<Move> weakCaptureMoves = new();
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
                if (pieceValues[(int)move.MovePieceType] < pieceValues[(int)move.CapturePieceType])
                {
                    bestCaptures.Add(move);
                }
                else if (pieceValues[(int)move.MovePieceType] == pieceValues[(int)move.CapturePieceType])
                {
                    equalCaptureMoves.Add(move);
                }
                else
                {
                    weakCaptureMoves.Add(move);
                }
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

        // Provide the best capture moves first
        foreach (Move move in bestCaptures) yield return move;

        // Then the equal capture moves
        foreach (Move move in equalCaptureMoves) yield return move;

        // Then the check moves
        foreach (Move move in checkMoves) yield return move;

        // Then the weak capture moves
        foreach (Move move in weakCaptureMoves) yield return move;

        // Finally the normal moves
        foreach (Move move in normalMoves) yield return move;
    }

    record move_score_t(Move move, int score)
    {
        override public string ToString() { return $"{move} score {score}::"; }
    }


    record SearchParams(Board board, bool isWhite, int max_depth, int millisecondsAllowedPerTurn, Timer timer, bool abort_search)
    {
        public bool abort_search { get; set; } = abort_search;
    }

    private (int, Move) get_best_move(SearchParams p, int depth, int alpha, int beta)
    {
        Move best_move_this_level = Move.NullMove;
        bool ourMove = p.isWhite == p.board.IsWhiteToMove;
        int best_score_this_level = ourMove ? int.MinValue : int.MaxValue;
        List<move_score_t> move_scores = new();

        depth++;
        bool checksAndCapturesOnly = depth > p.max_depth;
        var sortedMoves = GetSortedMoves(p.board, checksAndCapturesOnly);

        foreach (Move move in sortedMoves)
        {
            int score = 0;
            p.board.MakeMove(move);

            if (evaluated_positions.TryGetValue(p.board.ZobristKey, out var cached_entry) && cached_entry.depth >= p.max_depth)
            {
                score = cached_entry.score;
            }
            else
            {
                if (p.board.IsDraw())
                {
                    score = 0;
                }
                else if (p.board.IsInCheckmate())
                {
                    score = ourMove ? checkmateScore : -checkmateScore;
                }
                else
                {
                    (score, _) = get_best_move(p, depth, alpha, beta);

                    if (checksAndCapturesOnly)
                    {
                        int evalScore = Evaluate(p.board, p.isWhite);
                        score = ourMove ? Math.Max(score, evalScore) : Math.Min(score, evalScore);
                    }
                }

                evaluated_positions[p.board.ZobristKey] = new CacheEntry(depth, score);
            }
            p.board.UndoMove(move);

            if (p.max_depth > 1 && p.millisecondsAllowedPerTurn < p.timer.MillisecondsElapsedThisTurn)
            {
                p.abort_search = true;
                return (0, Move.NullMove);
            }

            if (ourMove)
            {
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
                if (score < best_score_this_level)
                {
                    best_score_this_level = score;
                    best_move_this_level = move;
                    beta = Math.Min(beta, score);
                    if (beta <= alpha)
                    {
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
            return (Evaluate(p.board, p.isWhite), Move.NullMove);
        }
    }

    public Move Think(Board board, Timer timer)
    {
        System.Console.WriteLine($"Thinking... FEN: {board.GetFenString()}");
        evaluationCount = 0;
        evaluated_positions.Clear();

        int millisecondsAllowedPerTurn = 600;
        int max_depth = 8;

        Move best_move = Move.NullMove;
        int best_score = 0;
        int alpha = int.MinValue;
        int beta = int.MaxValue;

        int depth = 1;
        bool abort_search = false;

        while (depth < max_depth && timer.MillisecondsElapsedThisTurn < millisecondsAllowedPerTurn)
        {
            var searchParams = new SearchParams(board, board.IsWhiteToMove, depth, millisecondsAllowedPerTurn, timer, abort_search);

            (int bs, Move bm) = get_best_move(searchParams, 0, alpha, beta);
            if (!searchParams.abort_search)
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

