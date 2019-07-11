using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DuplicateFinder.DataAccess;
using Microsoft.EntityFrameworkCore;
using static System.Console;

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
                        var sw0 = Stopwatch.StartNew();
                        if (!await db.Files.AnyAsync(x => x.FileName == fileInfo.FullName))
                        {
                            sw0.Stop();
                            _timeSpentOnDatabase += sw0.ElapsedMilliseconds;

                            string hash = await CreateHash(fileInfo);

                            var sw1 = Stopwatch.StartNew();
                            await db.Files.AddAsync(new Model.File
                            {
                                Id = Guid.NewGuid(),
                                Size = fileInfo.Length,
                                FileName = fileInfo.Name,
                                Path = fileInfo.FullName,
                                Hash = hash
                            });
                            sw1.Stop();
                            _timeSpentOnDatabase += sw1.ElapsedMilliseconds;
                        }
                        else
                        {
                            sw0.Stop();
                            _timeSpentOnDatabase += sw0.ElapsedMilliseconds;
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
                var sw2 = Stopwatch.StartNew();
                await db.SaveChangesAsync();
                sw2.Stop();
                _timeSpentOnDatabase += sw2.ElapsedMilliseconds;
            }
        }

        private static async Task<String> CreateHash(FileInfo fileInfo)
        {
            var result = String.Empty;
            var sw = Stopwatch.StartNew();
            using (var reader = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read))
            {
                using (var md5 = MD5.Create())
                {
                    if (fileInfo.Length > 1048576)
                    {
                        var buffer = new Byte[1048576];
                        await reader.ReadAsync(buffer, 0, 1048576);
                        var hashBytes = md5.ComputeHash(buffer);
                        result = Convert.ToBase64String(hashBytes);
                    }
                    else
                    {
                        var hashBytes = md5.ComputeHash(reader);
                        result = Convert.ToBase64String(hashBytes);
                    }
                }
            }

            sw.Stop();
            _timeSpentOnHashing += sw.ElapsedMilliseconds;

            return result;
        }

        private static Int32 _progress = 0;
        private static Int32 _lastProgress = 0;
        private static DateTime _startDateTime = DateTime.UtcNow;
        private static Int64 _timeSpentOnDatabase = 0;
        private static Int64 _timeSpentOnHashing = 0;

        private static void LogProgress()
        {
            _progress++;
            _lastProgress++;

            if ((DateTime.UtcNow - _startDateTime).TotalMinutes > 1)
            {
                WriteLine($"{_progress} items processed!");
                WriteLine($"{_lastProgress} items processed past minute!");
                WriteLine($"{_timeSpentOnHashing / 1000:F0} seconds spent on hashing past minute!");
                WriteLine($"{_timeSpentOnDatabase / 1000:F0} seconds spent on database writes past minute!");
                _startDateTime = DateTime.UtcNow;
                _lastProgress = 0;
                _timeSpentOnDatabase = 0;
                _timeSpentOnHashing = 0;
            }
        }
    }
}