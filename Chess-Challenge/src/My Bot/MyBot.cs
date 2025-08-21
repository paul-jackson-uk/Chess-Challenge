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
    private uint numLegalMovesCalls = 0;
    private const int checkmateScore = 1000000;
    public static int evaluationCount = 0;
    private Dictionary<ulong,int> evaluated_positions = new Dictionary<ulong, int>();
    private int[] pieceValues = { 0, 100, 300, 300, 500, 900, 20000 }; // none, Pawn, Knight, Bishop, Rook, Queen, King

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
        evaluationCount++;
        bool isEndGame = false;

        PieceList[] pieceLists = board.GetAllPieceLists();

        // Get opponent attacking peice count
        int opponentAttackingPieceCount = 0;
        IEnumerable<PieceList> opponentPieceQuery =
            from piece_list in pieceLists
            where piece_list.IsWhitePieceList != isWhite
            where pieceValues[(int) piece_list.TypeOfPieceInList] > 250
            select piece_list;

        foreach (PieceList pl in opponentPieceQuery)
        {
            opponentAttackingPieceCount += pl.Count * pieceValues[(int)pl.TypeOfPieceInList];
        }

        isEndGame = opponentAttackingPieceCount < 700;
        
        // Simple evaluation function: count material balance
        int score = 0;
        foreach (PieceList pl in pieceLists)
        {
            // add up material value of pieces
            int value = pieceValues[(int)pl.TypeOfPieceInList];
            for (int i = 0; i < pl.Count; i++)
            {
                // Add positional score for pieces
                switch (pl.TypeOfPieceInList)
                {
                    case PieceType.Pawn:
                        break;
                    case PieceType.Knight:
                        // Add positional score for pieces
                        value += 8 * GetEdgeDistance(pl.GetPiece(i).Square.Index);
                        break;
                    case PieceType.Bishop:
                        value += 8 * GetEdgeDistance(pl.GetPiece(i).Square.Index);
                        break;
                    case PieceType.Rook:
                        break;
                    case PieceType.Queen:
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
            }

            // Add or subtract peice value based on colour
            if (pl.IsWhitePieceList != isWhite) value = -value;
            score += value * pl.Count;

        }

        return score;
    }

    IEnumerable<Move> GetSortedMoves(Board board, bool checksAndCapturesOnly)
    {
        Move[] moves = board.GetLegalMoves();
        List<Move> checkMoves = new List<Move>();
        List<Move> captureMoves = new List<Move>();
        List<Move> normalMoves = new List<Move>();

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            if (board.IsInCheckmate())
            {
                board.UndoMove(move);
                yield return move; // If this move results in checkmate, return it immediately
                yield break;
            }
            else if (board.IsInCheck())
            {
                checkMoves.Add(move);
            }
            else if (move.IsCapture)
            {
                captureMoves.Add(move);
            }
            else if (!checksAndCapturesOnly)
            {
                normalMoves.Add(move);
            }
 
            board.UndoMove(move);
        }

        // Provide the check moves first
        foreach (Move move in checkMoves)
        {
            yield return move;
        }

        // Then the capture moves
        foreach (Move move in captureMoves)
        {
            yield return move;
        }

        // Finally the normal moves
        foreach (Move move in normalMoves)
        {
            yield return move;
        }
    }
    

    private (int, Move) get_best_move(Board board, bool isWhite, int depth, int alpha, int beta)
    {
        Move best_move_this_level = Move.NullMove;
        int best_score_this_level = (isWhite == board.IsWhiteToMove) ? int.MinValue : int.MaxValue;

        if (--depth > -10)
        {
            var sortedMoves = GetSortedMoves(board, depth < 0);

            // Now start the analysis
            foreach (Move move in sortedMoves)
            {
                int score = 0;
                board.MakeMove(move);

                // Have already evaluated this position?
                if (evaluated_positions.TryGetValue(board.ZobristKey, out int cached_score))
                {
                    score = cached_score;
                }
                else
                {
                    if (board.IsDraw())
                    {
                        score = 0;
                    }
                    else if (board.IsInCheckmate())
                    {
                        score = isWhite == board.IsWhiteToMove ? -checkmateScore : checkmateScore;
                    }
                    else
                    {
                        (score, _) = get_best_move(board, isWhite, depth, alpha, beta);
                    }
                    evaluated_positions.TryAdd(board.ZobristKey, score);
                }
                board.UndoMove(move);

                if (isWhite == board.IsWhiteToMove)
                {
                    // our move
                    if (score > best_score_this_level)
                    {
                        best_score_this_level = score;
                        best_move_this_level = move;
                        alpha = Math.Max(alpha, score);
                        if (beta <= alpha)
                        {
                            // Beta cut-off
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
        evaluationCount = 0;
        evaluated_positions.Clear();
        var all_bb = board.AllPiecesBitboard;

        int depth = 2;

        switch (BitboardHelper.GetNumberOfSetBits(all_bb))
        {
            case < 6: depth = 10; break;
            case < 8: depth = 8; break;
            case < 10: depth = 6; break;
            case < 14: depth = 5; break;
            case < 18: depth = 3; break;
            default: depth = 2; break;
        }

        Move best_move = Move.NullMove;
        int best_score = 0;
        int alpha = int.MinValue;
        int beta = int.MaxValue;
        (best_score, best_move) = get_best_move(board, board.IsWhiteToMove, depth, alpha, beta);

        if (!board.GetLegalMoves().Contains(best_move))
        {
            System.Console.WriteLine("ERROR: Best move not in legal moves!");
            System.Console.WriteLine(" - Zobrist key: " + board.ZobristKey);
            throw new Exception("ERROR: I messed smt up");
        }
        return best_move;
    }
}