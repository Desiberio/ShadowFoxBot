using System;

namespace ShadowFoxBotNet
{
    public class RiotGamesAPIException : Exception 
    {
        public ErrorCode Error { get; private set; }
        public RiotGamesAPIException() { }

        public RiotGamesAPIException(string message) { }

        public RiotGamesAPIException(string message, ErrorCode error) : base(message)
        {
            Error = error;
        }

        public RiotGamesAPIException(string message, Exception inner)
            : base(message, inner) { }
    }
}
