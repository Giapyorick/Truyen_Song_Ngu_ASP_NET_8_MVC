using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace WebTruyenTranh.Models;

public partial class TruyenSongNguContext : DbContext
{
    public TruyenSongNguContext()
    {
    }

    public TruyenSongNguContext(DbContextOptions<TruyenSongNguContext> options)
        : base(options)
    {
    }

    public virtual DbSet<TblAdmin> TblAdmins { get; set; }

    public virtual DbSet<TblAuthor> TblAuthors { get; set; }

    public virtual DbSet<TblCategory> TblCategories { get; set; }

    public virtual DbSet<TblCategoryOfStory> TblCategoryOfStories { get; set; }

    public virtual DbSet<TblChapter> TblChapters { get; set; }

    public virtual DbSet<TblChapterComment> TblChapterComments { get; set; }

    public virtual DbSet<TblComment> TblComments { get; set; }

    public virtual DbSet<TblParagraph> TblParagraphs { get; set; }

    public virtual DbSet<TblStory> TblStories { get; set; }

    public virtual DbSet<TblUser> TblUsers { get; set; }

    public virtual DbSet<TblUserFollowStory> TblUserFollowStories { get; set; }

    public virtual DbSet<TblUserLiking> TblUserLikings { get; set; }

    public virtual DbSet<TblUserRating> TblUserRatings { get; set; }

    public virtual DbSet<TblUserReadingProgress> TblUserReadingProgresses { get; set; }

    public virtual DbSet<TblVocabulary> TblVocabularies { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
            //        => optionsBuilder.UseSqlServer("Data Source=DESKTOP-8T25JED;Initial Catalog=TruyenSongNgu;Integrated Security=True;Trust Server Certificate=True");

        }
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblAdmin>(entity =>
        {
            entity.HasKey(e => e.AdminId).HasName("PK__tblAdmin__719FE4E843CF3D0E");

            entity.ToTable("tblAdmin");

            entity.HasIndex(e => e.Username, "UQ__tblAdmin__536C85E44AFAD200").IsUnique();

            entity.Property(e => e.AdminId).HasColumnName("AdminID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status).HasDefaultValue(true);
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblAuthor>(entity =>
        {
            entity.HasKey(e => e.AuthorId);

            entity.ToTable("tblAuthor");

            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.AuthorName).HasMaxLength(50);
            entity.Property(e => e.Country).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(50);
            entity.Property(e => e.Gender).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<TblCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId);

            entity.ToTable("tblCategory");

            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<TblCategoryOfStory>(entity =>
        {
            entity.HasKey(e => e.CategoryOfStoryId).HasName("PK__tblCateg__7A175BD169EAD37F");

            entity.ToTable("tblCategoryOfStory");

            entity.HasIndex(e => new { e.CategoryId, e.StoryId }, "UQ__tblCateg__8AE116288418053F").IsUnique();

            entity.Property(e => e.CategoryOfStoryId).HasColumnName("CategoryOfStoryID");
            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");

            entity.HasOne(d => d.Category).WithMany(p => p.TblCategoryOfStories)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblCategoryOfStory_tblCategory");

            entity.HasOne(d => d.Story).WithMany(p => p.TblCategoryOfStories)
                .HasForeignKey(d => d.StoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblCategoryOfStory_tblStory");
        });

        modelBuilder.Entity<TblChapter>(entity =>
        {
            entity.HasKey(e => e.ChapterId).HasName("PK__tblChapt__0893A34A0530B0E7");

            entity.ToTable("tblChapter");

            entity.Property(e => e.ChapterId).HasColumnName("ChapterID");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Story).WithMany(p => p.TblChapters)
                .HasForeignKey(d => d.StoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__tblChapte__Story__4CA06362");
        });

        modelBuilder.Entity<TblChapterComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__tblChapt__C3B4DFAA3AD24B83");

            entity.ToTable("tblChapterComment");

            entity.Property(e => e.CommentId).HasColumnName("CommentID");
            entity.Property(e => e.ChapterId).HasColumnName("ChapterID");
            entity.Property(e => e.CreateAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status).HasDefaultValue(true);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Chapter).WithMany(p => p.TblChapterComments)
                .HasForeignKey(d => d.ChapterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__tblChapte__Chapt__30C33EC3");

            entity.HasOne(d => d.User).WithMany(p => p.TblChapterComments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__tblChapte__UserI__2FCF1A8A");
        });

        modelBuilder.Entity<TblComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__tblComme__C3B4DFAAEFD2F427");

            entity.ToTable("tblComment");

            entity.Property(e => e.CommentId).HasColumnName("CommentID");
            entity.Property(e => e.CountLikes)
                .HasDefaultValue(0)
                .HasColumnName("Count_Likes");
            entity.Property(e => e.CreateAd)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("CreateAD");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.UserName).HasMaxLength(100);

            entity.HasOne(d => d.Story).WithMany(p => p.TblComments)
                .HasForeignKey(d => d.StoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__tblCommen__Story__71D1E811");
        });

        modelBuilder.Entity<TblParagraph>(entity =>
        {
            entity.HasKey(e => e.ParagraphId);

            entity.ToTable("tblParagraph");

            entity.Property(e => e.ParagraphId).HasColumnName("ParagraphID");
            entity.Property(e => e.ChapterId).HasColumnName("ChapID");
            entity.Property(e => e.English).HasColumnType("ntext");
            entity.Property(e => e.Vietnamese).HasColumnType("ntext");

            entity.HasOne(d => d.Chap).WithMany(p => p.TblParagraphs)
                .HasForeignKey(d => d.ChapterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblParagraph_tblChapter");
        });

        modelBuilder.Entity<TblStory>(entity =>
        {
            entity.HasKey(e => e.StoryId);

            entity.ToTable("tblStory");

            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.CountFolower).HasColumnName("Count_folower");
            entity.Property(e => e.CountRate).HasColumnName("Count_rate");
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(50);

            entity.HasOne(d => d.Author).WithMany(p => p.TblStories)
                .HasForeignKey(d => d.AuthorId)
                .HasConstraintName("FK_tblStory_tblAuthor");
        });

        modelBuilder.Entity<TblUser>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("tblUser");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreateAd)
                .HasColumnType("datetime")
                .HasColumnName("CreateAD");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.Img).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Passwork).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.ResetToken)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("reset_token");
            entity.Property(e => e.Status).HasMaxLength(10);
            entity.Property(e => e.TokenExpiry)
                .HasColumnType("datetime")
                .HasColumnName("token_expiry");
        });

        modelBuilder.Entity<TblUserFollowStory>(entity =>
        {
            entity.HasKey(e => e.FollowId).HasName("PK__tblUserF__2CE8108EC154A014");

            entity.ToTable("tblUserFollowStory");

            entity.HasIndex(e => new { e.UserId, e.StoryId }, "UQ__tblUserF__8460E0AF2913C658").IsUnique();

            entity.Property(e => e.FollowId).HasColumnName("FollowID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Story).WithMany(p => p.TblUserFollowStories)
                .HasForeignKey(d => d.StoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblUserFollowStory_tblStory");

            entity.HasOne(d => d.User).WithMany(p => p.TblUserFollowStories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblUserFollowStory_tblUser");
        });

        modelBuilder.Entity<TblUserLiking>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId }).HasName("PK__tblUserL__8460E0AEB4C9A112");

            entity.ToTable("tblUserLiking");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.LikedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Story).WithMany(p => p.TblUserLikings)
                .HasForeignKey(d => d.StoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__tblUserLi__Story__208CD6FA");

            entity.HasOne(d => d.User).WithMany(p => p.TblUserLikings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__tblUserLi__UserI__1F98B2C1");
        });

        modelBuilder.Entity<TblUserRating>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId }).HasName("PK__tblUserR__8460E0AE38A9983A");

            entity.ToTable("tblUserRating");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.RatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Story).WithMany(p => p.TblUserRatings)
                .HasForeignKey(d => d.StoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__tblUserRa__Story__1BC821DD");

            entity.HasOne(d => d.User).WithMany(p => p.TblUserRatings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__tblUserRa__UserI__1AD3FDA4");
        });

        modelBuilder.Entity<TblUserReadingProgress>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId }).HasName("PK__tblUserR__8460E04844C6A252");

            entity.ToTable("tblUserReadingProgress");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<TblVocabulary>(entity =>
        {
            entity.HasKey(e => e.VocabId).HasName("PK__tblVocab__BF7A5E590330BD3D");

            entity.ToTable("tblVocabulary");

            entity.Property(e => e.VocabId).HasColumnName("VocabID");
            entity.Property(e => e.ExampleEn).HasColumnName("ExampleEN");
            entity.Property(e => e.ExampleVi).HasColumnName("ExampleVI");
            entity.Property(e => e.Meaning).HasMaxLength(255);
            entity.Property(e => e.Word).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
