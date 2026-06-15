namespace GitWorkTree.ViewModel
{
    public sealed class BranchInfo
    {
        public string Name { get; }
        public string FullRef { get; }
        public bool HasLinkedWorktree { get; }

        public BranchInfo(string name, string fullRef, bool hasLinkedWorktree)
        {
            Name = name;
            FullRef = fullRef;
            HasLinkedWorktree = hasLinkedWorktree;
        }

        public override string ToString() => Name;
    }
}
