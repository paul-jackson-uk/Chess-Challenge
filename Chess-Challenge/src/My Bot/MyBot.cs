using System.Collections.Generic;
using System.Data;
using System.Runtime.InteropServices;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    struct evaluation
    {
        public int score;
        public Move move;
    }
    private int Evaluate(Board board, bool isWhite)
    {
        ulong whitePieces = board.WhitePiecesBitboard;
        // Simple evaluation function: count material balance
        int score = 0;
        for (int i = 0; i < 64; i++)
        {
            Square square = new Square(i);
            Piece piece = board.GetPiece(square);
            if (piece.IsNull) continue;

            int pieceValue = 0;
            switch (piece.PieceType)
            {
                case PieceType.Pawn: pieceValue = 100; break;
                case PieceType.Knight: pieceValue = 320; break;
                case PieceType.Bishop: pieceValue = 330; break;
                case PieceType.Rook: pieceValue = 500; break;
                case PieceType.Queen: pieceValue = 900; break;
                case PieceType.King: pieceValue = 20000; break;
            }
            if (!piece.IsWhite)
            {
                pieceValue = -pieceValue;
            }

            score += isWhite ? pieceValue : -pieceValue;

        }

        if (isWhite && ((board.WhitePiecesBitboard & (1UL << 28)) != 0))
        {
            string diagram = board.CreateDiagram(true, false, false);

            System.Console.WriteLine("Position: " + diagram);
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

    private (int, Move) get_best_move(Board board, bool isWhite, uint depth)
    {
        if (depth-- == 0)
        {
            int score = Evaluate(board, isWhite);
            return (score, Move.NullMove);
        }

        Move[] moves = board.GetLegalMoves();
        Move best_move_this_level = Move.NullMove;
        int best_score_this_level = (isWhite == board.IsWhiteToMove) ? int.MinValue : int.MaxValue;
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int score;
            (score, _) = get_best_move(board, isWhite, depth);
            board.UndoMove(move);

            if (isWhite == board.IsWhiteToMove)
            {
                if (score > best_score_this_level)
                {
                    best_score_this_level = score;
                    best_move_this_level = move;
                }
            }
            else
            {
                if (score < best_score_this_level)
                {
                    best_score_this_level = score;
                    best_move_this_level = move;
                }
            }
        }

        return (best_score_this_level, best_move_this_level);
    }

    public Move Think(Board board, Timer timer)
    {
        uint depth = 3;
        bool isWhite = board.IsWhiteToMove;
        Move best_move = Move.NullMove;
        int best_score = isWhite ? int.MinValue : int.MaxValue;
        int evaluation = int.MinValue;
        (best_score, best_move) = get_best_move(board, isWhite, depth);
        System.Console.WriteLine($"Best move evaluation: {evaluation}, best_score {best_score}"  );
        return best_move;
    }
}