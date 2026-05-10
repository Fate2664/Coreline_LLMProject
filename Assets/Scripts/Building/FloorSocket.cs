using UnityEngine;

namespace Coreline
{
    public class FloorSocket : EdgeSocket
    {
       [SerializeField] bool acceptsFloorPieces =  true;

       public override bool CanAcceptPart(BuildPieceType pieceType) => pieceType == BuildPieceType.Floor && acceptsFloorPieces;
    }
}
