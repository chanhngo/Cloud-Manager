namespace Cloud.MegaNz
{
    using System;

    public interface INodePublic
    {
        long Size { get; }

        string Name { get; }
    }

    public interface INode : INodePublic, IEquatable<INode>
    {
        string Id { get; }

        string ParentId { get; }

        string Owner { get; }

        NodeType Type { get; }

        DateTime LastModificationDate { get; }
    }

    public interface INodeCrypto
    {
        byte[] Key { get; }

        byte[] SharedKey { get; }

        byte[] Iv { get; }

        byte[] MetaMac { get; }

        byte[] FullKey { get; }
    }

    public enum NodeType
    {
        File = 0,
        Directory,
        Root,
        Inbox,
        Trash
    }
}