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
    private const uint MaxMovesSinceCapture = 2;
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
            int pawnFiles = 0;
            for (int i = 0; i < pl.Count; i++)
            {
                // Add material value
                if (pl.TypeOfPieceInList is not PieceType.King) value = pieceValues[(int)pl.TypeOfPieceInList];

                var sq = pl.GetPiece(i).Square;

                // Add positional score for pieces
                switch (pl.TypeOfPieceInList)
                {
                    case PieceType.Pawn:
                        // encourage pawns in the centre
                        value += 20 * (GetEdgeDistance(sq.Index) - 1);

                        // Encourage pawn pushing in endgame
                        if (isEndGame) value += (board.IsWhiteToMove ? sq.Rank : 7 - sq.Rank) << 4;

                        // Look for doubled pawns
                        int pawnFileMask = 1 << sq.File;
                        if ((pawnFiles & pawnFileMask) != 0) value -= 40;
                        pawnFiles |= pawnFileMask;
                        break;
                    case PieceType.Knight:
                        // Add positional score for pieces
                        value += 8 * BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetKnightAttacks(pl.GetPiece(i).Square));
                        break;
                    case PieceType.Bishop:
                    case PieceType.Rook:
                        value += (4 * BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetSliderAttacks(pl.TypeOfPieceInList, sq, board)));
                        break;
                    case PieceType.Queen:
                        if (isEndGame)
                        {
                            // In endgame, we want the queen to be more active
                            value += 20 * GetEdgeDistance(sq.Index);
                        }
                        else if (board.PlyCount < 10)
                        {
                            var queenRank = sq.Rank;
                            if (!board.IsWhiteToMove) queenRank = 7 - queenRank;
                            value += 20 * (3 - queenRank);
                        }
                        break;
                    case PieceType.King:
                        if (isEndGame)
                        {
                            // In endgame, we want the king to be more active
                            value += 30 * GetEdgeDistance(sq.Index);
                        }
                        else
                        {
                            // Keep the king on the back rank ideally towards the corner
                            var kingSquare = board.GetKingSquare(board.IsWhiteToMove);
                            int kingRank = board.IsWhiteToMove ? kingSquare.Rank : 7 - kingSquare.Rank;
                            value += ((7 - kingRank) * 10) + (Math.Min(kingSquare.File, (7 - kingSquare.File)) << 3);
                        }
                        break;
                }

                // Add or subtract piece value based on colour
                if (pl.IsWhitePieceList != isWhite) value = -value;
                score += value;
            }
        }

#if false
        // Boost for having more legal moves
        int legal_moves_boost = 10 - board.GetLegalMoves().Length;
        if (board.IsWhiteToMove == isWhite) legal_moves_boost = -legal_moves_boost;
        score += legal_moves_boost;
#endif
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
                int pieceVal = pieceValues[(int)move.MovePieceType];
                int targetVal = pieceValues[(int)move.CapturePieceType];
                var capList = (pieceVal, targetVal) switch
                {
                    _ when (pieceVal < targetVal) => bestCaptures,
                    _ when (pieceVal == targetVal) => equalCaptureMoves,
                    _ => weakCaptureMoves,
                };

                capList.Add(move);
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


    public class MoveNode
   {
        public int? evaluation = null;
        public Dictionary<Move, MoveNode> children = new();

        public override string ToString() { return $"Value {evaluation}, {children.Count} children"; }
    }

    public static MoveNode CreateMoveNode()
    {
		return new MoveNode();
    }

    record SearchParams(Board board, bool isWhite, int max_depth, int millisecondsAllowedPerTurn, Timer timer, bool abort_allowed, bool abort_search)
    {
        public bool abort_allowed { get; set; } = abort_allowed;
        public bool abort_search { get; set; } = abort_search;
        public int max_depth { get; set; } = max_depth;
    }

    private (int, Move) get_best_move(SearchParams p, int depth, int alpha, int beta, MoveNode subtree, uint movesSinceCapture = 0)
    {
        Move best_move_this_level = Move.NullMove;
        bool ourMove = p.isWhite == p.board.IsWhiteToMove;
        int best_score_this_level = ourMove ? int.MinValue : int.MaxValue;

        depth++;
        movesSinceCapture++;
        // if the position is currently in-check we should consider all legal moves as the following move might be
        // a capture from a fork say. 
        bool checksAndCapturesOnly = depth > p.max_depth && !p.board.IsInCheck();

        // When we are in checksAndCapturesOnly search don't go very deep without a capture or it will go on forever potentially
        if (checksAndCapturesOnly && movesSinceCapture > MaxMovesSinceCapture)
        {
            return (Evaluate(p.board, p.isWhite), Move.NullMove);
        }

        var sortedMoves = GetSortedMoves(p.board, checksAndCapturesOnly);

        foreach (Move move in sortedMoves)
        {
            MoveNode node;
            int score = TryMove(p, depth, alpha, beta, movesSinceCapture, ourMove, checksAndCapturesOnly, move, out node);

            // Have we run out of time?
            p.abort_search = (p.abort_allowed) && (p.millisecondsAllowedPerTurn < p.timer.MillisecondsElapsedThisTurn);
            if (p.abort_search) return (0, Move.NullMove);

            node.evaluation = score;
            subtree.children[move] = node;

            // Have we found the best move
            bool new_best = ourMove ? (score > best_score_this_level) : (score < best_score_this_level);
            if (new_best)
            {
                best_score_this_level = score;
                best_move_this_level = move;
                if (ourMove) alpha = Math.Max(alpha, score); else beta = Math.Min(beta, score);

                // Does this move warrant a prune?
                if (beta <= alpha) return (best_score_this_level, best_move_this_level);
            }
        }

        // Have we got a best move to return?
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

    private int TryMove(SearchParams p, int depth, int alpha, int beta, uint movesSinceCapture, bool ourMove, bool checksAndCapturesOnly, Move move, out MoveNode node)
    {
        node = CreateMoveNode();
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
                (score, _) = get_best_move(p, depth, alpha, beta, node, move.IsCapture ? 0 : movesSinceCapture);
            }

            evaluated_positions[p.board.ZobristKey] = new CacheEntry(depth, score);
        }
        p.board.UndoMove(move);

        if (checksAndCapturesOnly)
        {
            // The player doesn't have to make any of the captures or checks so we should
            // also do an evaluation and see whether that is better for them
            int evalScore = Evaluate(p.board, p.isWhite);
            score = ourMove ? Math.Max(score, evalScore) : Math.Min(score, evalScore);
        }

        return score;
	}

	public Move Think(Board board, Timer timer)
    {
        System.Console.WriteLine($"Thinking... FEN: {board.GetFenString()}");
        evaluationCount = 0;
        evaluated_positions.Clear();

        int millisecondsAllowedPerTurn = 700;
        int max_depth = 8;

        Move best_move = Move.NullMove;
        int best_score = 0;
        int alpha = int.MinValue;
        int beta = int.MaxValue;

        int depth = 2;
        bool abort_search = false;

        var searchTree = CreateMoveNode();
        var searchParams = new SearchParams(board, board.IsWhiteToMove, depth, millisecondsAllowedPerTurn, timer, false, abort_search);
        while (depth < max_depth && timer.MillisecondsElapsedThisTurn < millisecondsAllowedPerTurn)
        {
            (int bs, Move bm) = get_best_move(searchParams, 0, alpha, beta, searchTree);
            if (!searchParams.abort_search)
            {
                best_move = bm;
                best_score = bs;
            }
            Console.WriteLine($"Depth: {depth}, Best Move: {best_move}, Score: {best_score}, Time taken: {timer.MillisecondsElapsedThisTurn}ms, Evaluations: {evaluationCount}\n");
            depth++;
            searchParams.max_depth = depth;
            searchParams.abort_allowed = true;
        }

        return best_move;
    }
}

