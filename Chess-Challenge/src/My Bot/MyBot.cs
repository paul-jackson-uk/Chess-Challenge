using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using ChessChallenge.API;
using System.Collections;
using System;
public class MyBot : IChessBot
{
    int [] pawn_square_scores = new int[] {
        0, 0, 0, 0, 0, 0, 0, 0,
        20, 20, 10, 0, 0, 20, 20, 20,
        10, 10, 10, 20, 20, 20, 20, 10,
        5 ,5 ,10 ,35 ,35 ,10 ,5 ,5 ,
        0 ,0 ,0 ,20 ,20 ,0 ,0 ,0 ,
        -5,-5,-10,-5,-5,-10,-5,-5,
        -10,-10,-20,-30,-30,-20,-10,-10,
        0, 0, 0, 0, 0, 0, 0, 0
    };

    int[] knight_square_scores = new int[] {
        -100, -50, -30, -30, -30, -30, -30, -100,
        -50, -50, -20, -20, -20, -20, -50, -50,
        -30, -20, 10, 10, 10, 10, -20, -30,
        -30, -20, 10, 15, 15, 10, -20, -30,
        -30, -10, 10, 15, 15, 10, 10, -30,
        -30, 0, 10, 10, 10, 10, 0, -30,
        -50, -50, -20, -20, -20, -20, -50, -50,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0
    };

    int[] rook_square_scores = new int[] {
        0, 0, 0, 0, 0, 0, 0, 0,
        5, 10, 10, 10, 10, 10, 10, 5,
        -5, 0, 0, 0, 0, 0, -5, -5,
        -5, -5, -5, -5, -5, -5, -5, -5,
        -5, -5, -5, -5, -5, -5, -5, -5,
        -10,-10,-10,-10,-10,-10,-10,-10,
        -20,-20,-20,-20,-20,-20,-20,-20,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0
    };

    int[] bishop_square_scores = new int[] {
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        10, 10, 10, 10, 10, 10, 10, 10,
        -5, -5, -5, -5, -5, -5, -5, -5,
        -5, -5, -5, -5, -5, -5, -5, -5,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0
    };

    int[] queen_square_scores = new int[] {
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        10, 10, 10, 10, 10, 10, 10, 10,
        -5, -5, -5, -5, -5, -5, -5, -5,
        -5, -5, -5, -5, -5, -5, -5, -5,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0
    }; 

    int[] king_square_scores = new int[] {
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        5 ,5 ,5 ,5 ,5 ,5 ,5 ,5 ,
        10,10,10,10,10,10,10,10,
        20,20,20,20,20,20,20,20,
        30,30,30,30,30,30,30,30,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0
    };
    int[] none_square_scores = new int[] {
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        0 ,0 ,0 ,0 ,0 ,0 ,0 ,0,
        1, 1, 1, 1, 1, 1, 1, 1
    };
    int[][] square_scores;

    public MyBot()
    {
        square_scores = new int[][]
        {
            none_square_scores,
            pawn_square_scores,
            knight_square_scores,
            bishop_square_scores,
            rook_square_scores,
            queen_square_scores,
            king_square_scores
        };
    }
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
            // add up material value of pieces
            int value = pieceValues[(int)pl.TypeOfPieceInList];
            if (pl.IsWhitePieceList != isWhite) value = -value;
            score += value * pl.Count;

            // Add a position score
            var square_scores = this.square_scores[(int)pl.TypeOfPieceInList];
            {
                // Add positional score for peices
                for (int i = 0; i < pl.Count; i++)
                {
                    Piece piece = pl.GetPiece(i);
                    int squareIndex = piece.Square.Index;
                    int sq = (piece.IsWhite) ? squareIndex : 63 - squareIndex;
                    if (isWhite == piece.IsWhite)
                    {
                        score += square_scores[sq];
                    }
                    else
                    {
                        score -= square_scores[sq];
                    }
                }
            }

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