﻿using NewGear.Trees.TrueTree;

namespace NARCSharp;

public class NARCFileSystem
{
    private NARC _narc;
    private readonly BranchNode<byte[]> _root;

    public NARCFileSystem(in NARC narc)
    {
        _narc = narc;
        _root = narc.RootNode;
    }

    public byte[] GetFile(string path)
    {
        LeafNode<byte[]>? node = _root.FindChildByPath<LeafNode<byte[]>>(path);

        if (node?.Contents is null)
            return Array.Empty<byte>();

        return node.Contents;
    }

    public bool TryGetFile(string path, out byte[] contents)
    {
        LeafNode<byte[]>? node = _root.FindChildByPath<LeafNode<byte[]>>(path);

        if (node?.Contents is null)
        {
            contents = Array.Empty<byte>();
            return false;
        }

        contents = node.Contents;
        return true;
    }

    /// <summary>
    /// Writes a file to the system, overriding it if the entry already exists.
    /// </summary>
    public void AddFile(string path, byte[] data)
    {
        LeafNode<byte[]>? node = _root.FindChildByPath<LeafNode<byte[]>>(path);

        if (node is not null)
        {
            node.Contents = data;
            return;
        }

        string[] parts = path.Split('/');
        BranchNode<byte[]> current = _root;

        for (int i = 0; i < parts.Length - 1 ; i++)
        {
            BranchNode<byte[]> child = new(parts[i]);

            current.AddChild(child);
            current = child;
        }

        current.AddChild(new LeafNode<byte[]>(parts[parts.Length - 1], data)); // Add Leaf with the name of the last part of the path
    }
    
    /// <summary>
    /// Writes a file to the root of the system, overriding it if the entry already exists.
    /// </summary>
    public void AddFileRoot(string name, byte[] data)
    {
        LeafNode<byte[]>? node = _root.ChildLeaves.Find((LeafNode<byte[]> node) => {return node.Name == name;});

        if (node is not null)
        {
            node.Contents = data;
            return;
        }
        _root.AddChild(new LeafNode<byte[]>(name, data));
    }

    /// <summary>
    /// Removes a file from the system.
    /// </summary>
    /// <returns></returns>
    public bool RemoveFile(string path)
    {
        LeafNode<byte[]>? leaf = _root.FindChildByPath<LeafNode<byte[]>>(path);

        if (leaf is null)
            return false;

        return RemoveFile(leaf);
    }

    /// <summary>
    /// Removes a directory if empty. Setting recursive to true deletes all files and directories inside it.
    /// </summary>
    /// <param name="recursive">Setting this parameter to true removes all child files and directories.</param>
    /// <returns>Whether the directory was removed successfully.</returns>
    public bool RemoveDirectory(string path, bool recursive = false)
    {
        BranchNode<byte[]>? branch = _root.FindChildByPath<BranchNode<byte[]>>(path);

        if (branch is null || (!recursive && branch.HasChildren))
            return false;

        return RemoveDirectory(branch);
    }

    public Dictionary<string, INode<byte[]>>? GetDirectoryContents(string path)
    {
        path = path.Trim();

        if (string.IsNullOrEmpty(path) || path == "/")
            return GetDirectoryContents(_root);

        BranchNode<byte[]>? branch = _root.FindChildByPath<BranchNode<byte[]>>(path);

        if (branch is null)
            return null;

        return GetDirectoryContents(branch);
    }

    public static Dictionary<string, INode<byte[]>> GetDirectoryContents(BranchNode<byte[]> dir)
    {
        Dictionary<string, INode<byte[]>> dict = new();

        foreach (INode<byte[]> node in dir)
            dict.Add(node.Name, node);

        return dict;
    }

    /// <summary>
    /// Searches through all directories and subdirectories and lists them into a string array.
    /// A path from where to start the search can be specified.
    /// </summary>
    public string[] ListDirectoryTree(string path = "")
    {
        BranchNode<byte[]>? startPath = _root;
        List<string> pathList = new();
        string relPath = path;

        if (path.Length > 0 && !relPath.EndsWith("/"))
            relPath += "/";

        if (string.IsNullOrEmpty(path))
            startPath = _root.FindChildByPath<BranchNode<byte[]>>(path);

        if (startPath is null)
            return Array.Empty<string>();

        RecursiveSearch(startPath);

        void RecursiveSearch(BranchNode<byte[]> node)
        {
            pathList.Add(relPath + node.Name);

            foreach (BranchNode<byte[]> child in node.ChildBranches)
            {
                relPath += child.Name + "/";

                RecursiveSearch(child);
            }
        }

        return pathList.ToArray();
    }

    /// <summary>
    /// Enumerates all files within the desired directory, which is the root by default.<br/>
    /// Note that this method does not go into subdirectories.
    /// </summary>
    /// <param name="directory">The directory to enumerate the files from.</param>
    public IEnumerable<(string Name, byte[] Contents)> EnumerateFiles(string directory = "/")
    {
        directory = directory.Trim();

        BranchNode<byte[]>? branch;

        if (string.IsNullOrEmpty(directory) || directory == "/")
            branch = _root;
        else
            branch = _root.FindChildByPath<BranchNode<byte[]>>(directory);

        if (branch is null)
            yield break;

        foreach (LeafNode<byte[]> child in branch.ChildLeaves)
            yield return (child.Name, child.Contents ?? Array.Empty<byte>());
    }

    public NARC ToNARC() => _narc;

    private static bool RemoveFile(LeafNode<byte[]> leaf) =>
        leaf.Parent?.RemoveChild(leaf) ?? false;

    private static bool RemoveDirectory(BranchNode<byte[]> branch)
    {
        foreach (INode<byte[]> node in branch)
            if (node is LeafNode<byte[]> childLeaf)
                RemoveFile(childLeaf);
            else if (node is BranchNode<byte[]> childBranch)
                RemoveDirectory(childBranch);

        return branch.Parent?.RemoveChild(branch) ?? false;
    }
}
