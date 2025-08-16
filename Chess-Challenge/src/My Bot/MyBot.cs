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

        // Simple evaluation function: count material balance
        int score = 0;
        PieceList[] pieceLists = board.GetAllPieceLists();
        foreach (PieceList pl in pieceLists)
        {
            // add up material value of pieces
            int value = pieceValues[(int)pl.TypeOfPieceInList];
            if (pl.TypeOfPieceInList is not (PieceType.Queen or PieceType.King or PieceType.Rook))
            {
                for (int i = 0; i < pl.Count; i++)
                {
                    // Add positional score for pieces
                    value += 8 * GetEdgeDistance(pl.GetPiece(i).Square.Index);
                }
            }

            // Add or subtract peice value based on colour
            if (pl.IsWhitePieceList != isWhite) value = -value;
            score += value * pl.Count;

        }


        return score;
    }

    
    private (int, Move) get_best_move(Board board, bool isWhite, int depth, int alpha, int beta)
    {

        Move best_move_this_level = Move.NullMove;
        int best_score_this_level = (isWhite == board.IsWhiteToMove) ? int.MinValue : int.MaxValue;

        if (board.IsInCheckmate())
        {
            return (isWhite == board.IsWhiteToMove ? int.MinValue : int.MaxValue, Move.NullMove);
        }
        else if (--depth > -12)
        {
            Move[] moves = board.GetLegalMoves(depth < 0);
            if (moves.Length > 1)
            {
                Array.Sort(moves, (a, b) =>
                {
                    if (a.IsCapture && !b.IsCapture) return -1;
                    if (!a.IsCapture && b.IsCapture) return 1;
                    return 0;
                });
            }

            foreach (Move move in moves)
            {
                int score = 0;
                board.MakeMove(move);
                try
                {
                    if (board.IsDraw())
                    {
                        score = 0;
                    }
                    else
                    {
                        // Have already evaluated this position?
                        if (evaluated_positions.TryGetValue(board.ZobristKey, out int cached_score))
                        {
                            score = cached_score;
                        }
                        else
                        {
                            (score, _) = get_best_move(board, isWhite, depth, alpha, beta);
                            evaluated_positions.TryAdd(board.ZobristKey, score);
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("Exception: " + e.Message);
                    System.Console.WriteLine(board.CreateDiagram());
                }
                finally
                {
                    board.UndoMove(move);
                }

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
            int evaluation = Evaluate(board, isWhite);

            // We must have looked at no moves because we are at max depth and there are no captures or checks
            return (evaluation, Move.NullMove);
        }
    }

    public Move Think(Board board, Timer timer)
    {
        System.Console.WriteLine("Thinking...");
        if (board.IsInCheckmate() || board.IsDraw())
        {
            System.Console.WriteLine("Checkmate or draw detected.");
            //return Move.NullMove; // No valid moves if in checkmate or draw
        }
        evaluationCount = 0;
        evaluated_positions.Clear();
        var all_bb = board.AllPiecesBitboard;

        int depth = 2;

        switch (BitboardHelper.GetNumberOfSetBits(all_bb))
        {
            case < 6: depth = 12; break;
            case < 8: depth = 10; break;
            case < 10: depth = 8; break;
            case < 14: depth = 6; break;
            case < 18: depth = 4; break;
            default: depth = 2; break;
        }

        bool isWhite = board.IsWhiteToMove;
        Move best_move = Move.NullMove;
        int best_score = isWhite ? int.MinValue : int.MaxValue;
        int alpha = int.MinValue;
        int beta = int.MaxValue;
        (best_score, best_move) = get_best_move(board, isWhite, depth, alpha, beta);
        System.Console.Write($"Best move score {best_score}"  );

        if (!board.GetLegalMoves().Contains(best_move))
        {
            throw new Exception("ERROR: I messed smt up");
        }
        System.Console.WriteLine(" - Best move: " + best_move.ToString() + " - Evaluation count: " + evaluationCount);
        return best_move;
    }
}