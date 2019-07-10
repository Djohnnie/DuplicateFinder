using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DuplicateFinder.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DuplicateFinder
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var locationsVariable = Environment.GetEnvironmentVariable("LOCATIONS");
            var locations = locationsVariable.Split(';');
            using (var db = new DatabaseContext())
            {
                await db.Database.EnsureCreatedAsync();
            }

            foreach (var location in locations)
            {
                var directoryInfo = new DirectoryInfo(location);
                await Recurse(directoryInfo);
            }
        }

        static async Task Recurse(DirectoryInfo directoryInfo)
        {
            foreach (var subDirectoryInfo in directoryInfo.GetDirectories())
            {
                await Recurse(subDirectoryInfo);
            }

            using (var db = new DatabaseContext())
            {
                foreach (var fileInfo in directoryInfo.GetFiles())
                {
                    try
                    {
                        if (!await db.Files.AnyAsync(x => x.FileName == fileInfo.FullName))
                        {
                            await db.Files.AddAsync(new Model.File
                            {
                                Id = Guid.NewGuid(),
                                Size = fileInfo.Length,
                                FileName = fileInfo.Name,
                                Path = fileInfo.FullName,
                                Hash = CreateHash(fileInfo)
                            });
                        }
                    }
                    catch
                    {
                        // Nothing we can do...
                    }
                }

                await db.SaveChangesAsync();
            }
        }

        private static String CreateHash(FileInfo fileInfo)
        {
            using (var reader = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read))
            {
                using (var md5 = MD5.Create())
                {
                    var hashBytes = md5.ComputeHash(reader);
                    return Convert.ToBase64String(hashBytes);
                }
            }
        }
    }
}