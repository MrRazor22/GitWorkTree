namespace GitWorkTree.Commands
{
    public enum CommandActions { Create, Remove, Prune, Open, Cancel };
    public class CommandActionsEventArgs : EventArgs
    {
        public CommandActions commandAction { get; }
        public CommandActionsEventArgs(CommandActions commandAction)
        {
            this.commandAction = commandAction;
        }
    }
}