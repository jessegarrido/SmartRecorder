using Microsoft.EntityFrameworkCore;
using SQLitePCL;
using System;
using System.Collections.Generic;

namespace SmartRecorder.Data
{
    public interface IDataRepository 
    {
        public void AddWavTake(WavTake wavTake);
        public void AddMp3Take(Mp3Take mp3Take);
        public void MarkUploaded(int mp3TakeId);
        public void MarkNormalized(int wavTakeId);
        public void AddMp3TagSet(Mp3TagSet mp3TagSet);
        public void SetDefaultMp3TagSet(int mp3TagSetId);
    }
    public class DataRepository : IDataRepository
    {
        private readonly TakeContext _context = new TakeContext();
        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }
        public void AddWavTake(WavTake wavTake)
        {
            _context.Add<WavTake>(wavTake);
            _context.SaveChanges();
        }
        public void AddMp3Take(Mp3Take mp3Take)
        {
            _context.Add<Mp3Take>(mp3Take);
            _context.SaveChanges();
        }
        public void AddMp3TagSet(Mp3TagSet mp3TagSet)
        {
            _context.Add<Mp3TagSet>(mp3TagSet);
            _context.SaveChanges();
        }
        public void MarkUploaded(int mp3TakeId)
        {
            var mp3Take = _context.Mp3Takes
                .Where(t => t.Mp3TakeId == mp3TakeId).FirstOrDefault();
            mp3Take.IsUpLoaded = true;
            _context.SaveChanges();
        }
        public void MarkNormalized(int wavTakeId)
        {
            var wavTake = _context.WavTakes
                .Where(t => t.WavTakeId == wavTakeId).FirstOrDefault();
            wavTake.IsNormalized = true;
            _context.SaveChanges();
        }
        public void SetDefaultMp3TagSet(int mp3TagSetId)
        {
            var mp3TagSet = _context.Mp3TagSets
                .Where(t => t.Mp3TagSetId == mp3TagSetId).FirstOrDefault();
             mp3TagSet.IsDefault = true;
            _context.SaveChanges();
        }
    }

}
    
    public class TakeContext : DbContext
    {
        public DbSet<WavTake> WavTakes { get; set; }
        public DbSet<Mp3Take> Mp3Takes { get; set; }
        public DbSet<Mp3TagSet> Mp3TagSets { get; set; }
        public string DbPath { get; }

        public TakeContext()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = System.IO.Path.Join(path, "db.db");
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
    }
public class WavTake
{
    public int WavTakeId { get; set; }
    public int RunLengthInSeconds { get; set; }
    public decimal OriginalPeakVolume { get; set; }
    public string FileName { get; set; } = string.Empty;
    public bool IsNormalized { get; set; }
    public bool WasConvertedToMp3 { get; set; }
    // public int CreationDate { get; set; }

    //public string Url { get; set; }

    // public WavTakeI <Post> Posts { get; } = new();
}

public class Mp3Take
{
    public int Mp3TakeId { get; set; }
    public string Title { get; set; }
    public string FileName { get; set; }
    public bool IsUpLoaded { get; set; }
    // public int CreationDate { get; set; }
    //  public string Content { get; set; }

    //  public int BlogId { get; set; }
    //  public Blog Blog { get; set; }
}
public class Mp3TagSet
{
    public int Mp3TagSetId { get; set; }
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Album { get; set; }
    public bool IsDefault { get; set; }

    //  public string Content { get; set; }

    //  public int BlogId { get; set; }
    //  public Blog Blog { get; set; }
}
