namespace GitWorkTree.ViewModel
{
    public sealed class BranchInfo
    {
        public string Name { get; }
        public string FullRef { get; }
        public bool HasLinkedWorktree { get; }
        public bool IsTag { get; }

        public BranchInfo(string name, string fullRef, bool hasLinkedWorktree, bool isTag = false)
        {
            Name = name;
            FullRef = fullRef;
            HasLinkedWorktree = hasLinkedWorktree;
            IsTag = isTag;
        }

        public override string ToString() => Name;
    }
}
