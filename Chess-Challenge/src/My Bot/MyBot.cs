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

    private int get_best_move(Board board, bool isWhite, uint depth, Move bestMove, ref int bestScore, bool topLevel)
    {
        if (depth-- == 0)
        {
            int score = Evaluate(board, isWhite);
            if (score == 1600)
            {
                string diagram = board.CreateDiagram(true, false, false);
                System.Console.WriteLine("Score 1600, position " + diagram);
                System.Console.WriteLine("White pieces bitbard: " + board.WhitePiecesBitboard);

            }
            return score;
        }

        Move[] moves = board.GetLegalMoves();
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int score = get_best_move(board, isWhite, depth, bestMove, ref bestScore, false);
            board.UndoMove(move);

            if (board.IsWhiteToMove == isWhite)
            {
                // our move
                if (score > bestScore)
                {
                    string diagram = board.CreateDiagram(true, false, false);
                    System.Console.WriteLine($"Best position white move, score {score}\n{diagram}");
                    bestScore = score;
                    if (topLevel)
                    {
                        bestMove = move; // Update the best move at the top level
                    }
                }
            }
            else
            {
                // opponent's move
                if (score < bestScore)
                {
                    string diagram = board.CreateDiagram(true, false, false);
                    System.Console.WriteLine($"Best position black move {diagram}");
                    bestScore = score;
                }
            }
        }

        return bestScore;
    }

    public Move Think(Board board, Timer timer)
    {
        uint depth = 3;
        bool isWhite = board.IsWhiteToMove;
        Move best_move = Move.NullMove;
        int evaluation = int.MinValue;
        int best_score = get_best_move(board, isWhite, depth, best_move, ref evaluation, true);
        System.Console.WriteLine($"Best move evaluation: {evaluation}, best_score {best_score}"  );
        return best_move;
    }
}