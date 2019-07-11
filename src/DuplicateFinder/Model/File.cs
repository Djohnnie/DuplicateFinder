using System;

namespace DuplicateFinder.Model
{
    public class File
    {
        public Guid Id { get; set; }
        public Int32 SysId { get; set; }
        public Int64 Size { get; set; }
        public String FileName { get; set; }
        public String Path { get; set; }
        public Guid PathHash { get; set; }
        public Guid DataHash { get; set; }
    }
}