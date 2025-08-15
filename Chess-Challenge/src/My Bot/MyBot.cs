using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using ChessChallenge.API;
using System.Collections;
using System;
public class MyBot : IChessBot
{

    public static int evaluationCount = 0;
    private Dictionary<ulong,int> evaluated_positions = new Dictionary<ulong, int>();
    private int[] pieceValues = { 0, 100, 300, 300, 500, 900, 20000 }; // none, Pawn, Knight, Bishop, Rook, Queen, King
    private int Evaluate(Board board, bool isWhite)
    {
        evaluationCount++;

        // Simple evaluation function: count material balance
        int score = 0;
        PieceList[] pieceLists = board.GetAllPieceLists();
        foreach (PieceList pl in pieceLists)
        {
            int value = pieceValues[(int) pl.TypeOfPieceInList];

            if (pl.IsWhitePieceList != isWhite) value = -value;

            score += value * pl.Count;
        }

        if (isWhite && ((board.WhitePiecesBitboard & (1UL << 28)) != 0))
        {
            // Example of a positional bonus for White's King being on e1
            score += 50; // Arbitrary bonus for pawn centre 
        }
        if (isWhite && ((board.WhitePiecesBitboard & (1UL << 29)) != 0))
        {
            // Example of a positional bonus for White's King being on e1
            score += 50; // Arbitrary bonus for pawn centre 
        }

        return score;
    }

    private (int, Move) get_best_move(Board board, bool isWhite, int depth, int alpha, int beta)
    {

        Move best_move_this_level = Move.NullMove;
        int best_score_this_level = (isWhite == board.IsWhiteToMove) ? int.MinValue : int.MaxValue;

        depth--;
        Move[] moves = board.GetLegalMoves(depth < 0);

        foreach (Move move in moves)
        {
            int score;
            board.MakeMove(move);
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
        evaluationCount = 0;
        evaluated_positions.Clear();
        int depth = 4;
        bool isWhite = board.IsWhiteToMove;
        Move best_move = Move.NullMove;
        int best_score = isWhite ? int.MinValue : int.MaxValue;
        int alpha = int.MinValue;
        int beta = int.MaxValue;
        (best_score, best_move) = get_best_move(board, isWhite, depth, alpha, beta);
        System.Console.Write($"Best move score {best_score}"  );
        System.Console.Write($"Total evaluations: {evaluationCount} ");
        Move[] history = board.GameMoveHistory.ToArray();
        foreach (Move m in history)
        {
            System.Console.Write($" {m.ToString()}");
        }
        System.Console.WriteLine("");
        return best_move;
    }
}