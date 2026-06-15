namespace GitWorkTree.ViewModel
{
    public sealed class BranchInfo
    {
        public string Name { get; }
        public bool HasLinkedWorktree { get; }

        public BranchInfo(string name, bool hasLinkedWorktree)
        {
            Name = name;
            HasLinkedWorktree = hasLinkedWorktree;
        }

        public override string ToString() => Name;
    }
}
