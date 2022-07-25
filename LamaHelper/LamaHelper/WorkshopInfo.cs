using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LamaHelper
{
    internal class WorkshopInfo
    {
        public string Name { get; set; } = string.Empty;
        public string[] Zeitslots { get; set; } = Array.Empty<string>();
        public string Treffpunkt { get; set; } = string.Empty;
        public string[] Zielgruppen { get; set; } = Array.Empty<string>();
        public string Veranstalter { get; set; } = string.Empty;
        public string Kurzbeschreibung { get; set; } = string.Empty;
        public string Beschreibung { get; set; } = string.Empty;
    }
}
