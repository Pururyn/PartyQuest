using Unity.Netcode;
using Unity.Collections;

public struct PlayerData : INetworkSerializable, System.IEquatable<PlayerData>
{
    public ulong ClientId;     // ID du client (si humain)
    public int CharacterId;    // Quel personnage (0=Mario, etc.)
    public bool IsAI;          // Est-ce un bot ?
    public FixedString32Bytes PlayerName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref CharacterId);
        serializer.SerializeValue(ref IsAI);
        serializer.SerializeValue(ref PlayerName);
    }

    public bool Equals(PlayerData other) => ClientId == other.ClientId && IsAI == other.IsAI;
}