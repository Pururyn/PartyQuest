using Unity.Netcode;
using Unity.Collections;

public struct PlayerData : INetworkSerializable, System.IEquatable<PlayerData>
{
    public ulong ClientId;
    public int CharacterId;
    public bool IsAI;
    public FixedString32Bytes PlayerName;

    // NOUVEAU : Le score est stocké ici
    public int Score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref CharacterId);
        serializer.SerializeValue(ref IsAI);
        serializer.SerializeValue(ref PlayerName);

        // NOUVEAU : On n'oublie pas de sérialiser le score !
        serializer.SerializeValue(ref Score);
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId &&
               IsAI == other.IsAI &&
               Score == other.Score; // Le changement de score déclenchera la détection de modification
    }
}