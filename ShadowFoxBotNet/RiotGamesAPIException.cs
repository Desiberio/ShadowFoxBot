using System;

namespace ShadowFoxBotNet
{
    [Serializable]
    public class RiotGamesAPIException : Exception 
    {
        public int errorCode { get; }
        public RiotGamesAPIException() { }

        public RiotGamesAPIException(string message)
            : base(message) { }

        public RiotGamesAPIException(string message, int errorCode)
            : this(message) 
        {
            this.errorCode = errorCode;
        }

        public RiotGamesAPIException(string message, Exception inner)
            : base(message, inner) { }
    }
}
