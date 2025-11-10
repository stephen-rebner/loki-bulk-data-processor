namespace LokiBulkDataProcessor.Core.UnitTests.TestModels
{
    public class InvalidModelObject
    {
        private bool PrivateBool { get; set; }

        internal bool InternalBool { get; set; }

        protected bool ProtectedBool { get; set; }

        public bool PrivateSetBool { get; private set; }

        public bool ProtectedSetBool { get; protected set; }
    }
}
