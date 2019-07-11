using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
            try
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


                WriteLine("<><><> FINISHED <><><>");
            }
            catch(Exception ex)
            {
                WriteLine($"MAIN ERROR: {ex}");
            }
        }

        static async Task Recurse(DirectoryInfo directoryInfo)
        {
            try
            {
                foreach (var subDirectoryInfo in directoryInfo.GetDirectories())
                {
                    await Recurse(subDirectoryInfo);
                }

                var sw = Stopwatch.StartNew();
                using (var db = new DatabaseContext())
                {
                    sw.Stop();
                    _timeSpentOnDatabase += sw.ElapsedMilliseconds;
                    foreach (var fileInfo in directoryInfo.GetFiles())
                    {
                        _lastProcessedFilePath = fileInfo.FullName;
                        try
                        {
                            var pathHash = CreateHash(fileInfo.FullName);

                            var sw0 = Stopwatch.StartNew();
                            if (!await db.Files.AnyAsync(x => x.PathHash == pathHash))
                            {
                                sw0.Stop();
                                _timeSpentOnDatabase += sw0.ElapsedMilliseconds;

                                var dataHash = await CreateHash(fileInfo);

                                var sw1 = Stopwatch.StartNew();
                                await db.Files.AddAsync(new Model.File
                                {
                                    Id = Guid.NewGuid(),
                                    Size = fileInfo.Length,
                                    FileName = fileInfo.Name,
                                    Path = fileInfo.FullName,
                                    PathHash = pathHash,
                                    DataHash = dataHash
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
                            WriteLine($"FILE ERROR: {ex}");
                        }

                        LogProgress();
                    }

                    var sw2 = Stopwatch.StartNew();
                    await db.SaveChangesAsync();
                    sw2.Stop();
                    _timeSpentOnDatabase += sw2.ElapsedMilliseconds;
                }
            }
            catch (Exception ex)
            {
                WriteLine($"RECURSE ERROR: {ex}");
            }
        }

        private static Guid CreateHash(String path)
        {
            var result = Guid.Empty;
            var sw = Stopwatch.StartNew();
            using (var md5 = MD5.Create())
            {
                var stringBytes = Encoding.UTF32.GetBytes(path);
                var hashBytes = md5.ComputeHash(stringBytes);
                result = new Guid(hashBytes);
            }

            sw.Stop();
            _timeSpentOnHashing += sw.ElapsedMilliseconds;

            return result;
        }

        private static async Task<Guid> CreateHash(FileInfo fileInfo)
        {
            try
            {
                var result = Guid.Empty;
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
                            result = new Guid(hashBytes);
                        }
                        else
                        {
                            var hashBytes = md5.ComputeHash(reader);
                            result = new Guid(hashBytes);
                        }
                    }
                }

                sw.Stop();
                _timeSpentOnHashing += sw.ElapsedMilliseconds;

                return result;
            }
            catch(Exception ex)
            {
                WriteLine($"HASH ERROR: {ex}");
                throw;
            }
        }

        private static Int32 _progress = 0;
        private static Int32 _lastProgress = 0;
        private static DateTime _startDateTime = DateTime.UtcNow;
        private static Int64 _timeSpentOnDatabase = 0;
        private static Int64 _timeSpentOnHashing = 0;
        private static String _lastProcessedFilePath;

        private static void LogProgress()
        {
            _progress++;
            _lastProgress++;

            if ((DateTime.UtcNow - _startDateTime).TotalMinutes > 1)
            {
                WriteLine($"{_progress} items processed!");
                WriteLine($"{_lastProgress} items processed past minute!");
                WriteLine($"{_timeSpentOnHashing / 1000:F0} seconds spent on hashing past minute!");
                WriteLine($"{_timeSpentOnDatabase / 1000:F0} seconds spent on database queries past minute!");
                WriteLine($"Last processed file: {_lastProcessedFilePath}");
                WriteLine("------------------------------------------------------------------------------");
                _startDateTime = DateTime.UtcNow;
                _lastProgress = 0;
                _timeSpentOnDatabase = 0;
                _timeSpentOnHashing = 0;
            }
        }
    }
}