namespace JKClient {
    public class EntityEventArgs {
        public ClientGame.EntityEvent Event { get; internal init; }
        public ClientEntity Entity { get; internal init; }
        internal EntityEventArgs(ClientGame.EntityEvent ev, ref ClientEntity cent) {
            this.Event = ev;
            this.Entity = cent;
        }
    }
}