using UnityEngine;

namespace Coreline
{
    public class WallSocket : EdgeSocket
    {
        [SerializeField] bool acceptsWallPieces = true;
        [SerializeField] bool acceptsDoorPieces = true;

        public override bool CanAcceptPart(BuildPieceType pieceType) => pieceType switch
        {
            BuildPieceType.Wall => acceptsWallPieces,
            BuildPieceType.Door => acceptsDoorPieces,
            _ => false
        };
    }
}
