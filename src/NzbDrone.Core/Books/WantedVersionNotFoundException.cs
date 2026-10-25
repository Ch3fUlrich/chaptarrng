using System;

namespace NzbDrone.Core.Books
{
    public class WantedVersionNotFoundException : Exception
    {
        public WantedVersionNotFoundException(string message) : base(message)
        {
        }
    }
}
