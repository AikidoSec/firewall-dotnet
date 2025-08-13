using System;
using System.Collections.Generic;
using System.Text;

namespace Aikido.Zen.Core.Models
{
    public class Package
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public long RequiredAt { get; set; }
    }
}
