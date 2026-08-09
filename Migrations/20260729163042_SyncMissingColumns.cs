using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebTruyenTranh.Migrations
{
    /// <inheritdoc />
    public partial class SyncMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tblAdmin",
                columns: table => new
                {
                    AdminID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Password = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Status = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblAdmin__719FE4E843CF3D0E", x => x.AdminID);
                });

            migrationBuilder.CreateTable(
                name: "tblAuthor",
                columns: table => new
                {
                    AuthorID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DoB = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Img = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblAuthor", x => x.AuthorID);
                });

            migrationBuilder.CreateTable(
                name: "tblCategory",
                columns: table => new
                {
                    CategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblCategory", x => x.CategoryID);
                });

            migrationBuilder.CreateTable(
                name: "tblUser",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DoB = table.Column<DateOnly>(type: "date", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Img = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreateAD = table.Column<DateTime>(type: "datetime", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Passwork = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    reset_token = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    token_expiry = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblUser", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "tblUserReadingProgress",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    LastChapterId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblUserR__8460E04844C6A252", x => new { x.UserId, x.StoryId });
                });

            migrationBuilder.CreateTable(
                name: "tblVocabulary",
                columns: table => new
                {
                    VocabID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Word = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Meaning = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ExampleEN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExampleVI = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblVocab__BF7A5E590330BD3D", x => x.VocabID);
                });

            migrationBuilder.CreateTable(
                name: "tblStory",
                columns: table => new
                {
                    StoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuthorID = table.Column<int>(type: "int", nullable: true),
                    PublicationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Img = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Likes = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rate = table.Column<double>(type: "float", nullable: true),
                    Count_folower = table.Column<int>(type: "int", nullable: true),
                    Count_rate = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblStory", x => x.StoryID);
                    table.ForeignKey(
                        name: "FK_tblStory_tblAuthor",
                        column: x => x.AuthorID,
                        principalTable: "tblAuthor",
                        principalColumn: "AuthorID");
                });

            migrationBuilder.CreateTable(
                name: "tblCategoryOfStory",
                columns: table => new
                {
                    CategoryOfStoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryID = table.Column<int>(type: "int", nullable: false),
                    StoryID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblCateg__7A175BD169EAD37F", x => x.CategoryOfStoryID);
                    table.ForeignKey(
                        name: "FK_tblCategoryOfStory_tblCategory",
                        column: x => x.CategoryID,
                        principalTable: "tblCategory",
                        principalColumn: "CategoryID");
                    table.ForeignKey(
                        name: "FK_tblCategoryOfStory_tblStory",
                        column: x => x.StoryID,
                        principalTable: "tblStory",
                        principalColumn: "StoryID");
                });

            migrationBuilder.CreateTable(
                name: "tblChapter",
                columns: table => new
                {
                    ChapterID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryID = table.Column<int>(type: "int", nullable: false),
                    ChapterNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblChapt__0893A34A0530B0E7", x => x.ChapterID);
                    table.ForeignKey(
                        name: "FK__tblChapte__Story__4CA06362",
                        column: x => x.StoryID,
                        principalTable: "tblStory",
                        principalColumn: "StoryID");
                });

            migrationBuilder.CreateTable(
                name: "tblComment",
                columns: table => new
                {
                    CommentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryID = table.Column<int>(type: "int", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Contents = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateAD = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    Count_Likes = table.Column<int>(type: "int", nullable: true, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblComme__C3B4DFAAEFD2F427", x => x.CommentID);
                    table.ForeignKey(
                        name: "FK__tblCommen__Story__71D1E811",
                        column: x => x.StoryID,
                        principalTable: "tblStory",
                        principalColumn: "StoryID");
                });

            migrationBuilder.CreateTable(
                name: "tblUserFollowStory",
                columns: table => new
                {
                    FollowID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    StoryID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblUserF__2CE8108EC154A014", x => x.FollowID);
                    table.ForeignKey(
                        name: "FK_tblUserFollowStory_tblStory",
                        column: x => x.StoryID,
                        principalTable: "tblStory",
                        principalColumn: "StoryID");
                    table.ForeignKey(
                        name: "FK_tblUserFollowStory_tblUser",
                        column: x => x.UserID,
                        principalTable: "tblUser",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "tblUserLiking",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false),
                    StoryID = table.Column<int>(type: "int", nullable: false),
                    Liking = table.Column<int>(type: "int", nullable: false),
                    LikedDate = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblUserL__8460E0AEB4C9A112", x => new { x.UserID, x.StoryID });
                    table.ForeignKey(
                        name: "FK__tblUserLi__Story__208CD6FA",
                        column: x => x.StoryID,
                        principalTable: "tblStory",
                        principalColumn: "StoryID");
                    table.ForeignKey(
                        name: "FK__tblUserLi__UserI__1F98B2C1",
                        column: x => x.UserID,
                        principalTable: "tblUser",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "tblUserRating",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false),
                    StoryID = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    RatedDate = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblUserR__8460E0AE38A9983A", x => new { x.UserID, x.StoryID });
                    table.ForeignKey(
                        name: "FK__tblUserRa__Story__1BC821DD",
                        column: x => x.StoryID,
                        principalTable: "tblStory",
                        principalColumn: "StoryID");
                    table.ForeignKey(
                        name: "FK__tblUserRa__UserI__1AD3FDA4",
                        column: x => x.UserID,
                        principalTable: "tblUser",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "tblChapterComment",
                columns: table => new
                {
                    CommentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    ChapterID = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    Status = table.Column<bool>(type: "bit", nullable: true, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tblChapt__C3B4DFAA3AD24B83", x => x.CommentID);
                    table.ForeignKey(
                        name: "FK__tblChapte__Chapt__30C33EC3",
                        column: x => x.ChapterID,
                        principalTable: "tblChapter",
                        principalColumn: "ChapterID");
                    table.ForeignKey(
                        name: "FK__tblChapte__UserI__2FCF1A8A",
                        column: x => x.UserID,
                        principalTable: "tblUser",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "tblParagraph",
                columns: table => new
                {
                    ParagraphID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChapID = table.Column<int>(type: "int", nullable: false),
                    ParagraphOrder = table.Column<int>(type: "int", nullable: false),
                    English = table.Column<string>(type: "ntext", nullable: false),
                    Vietnamese = table.Column<string>(type: "ntext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblParagraph", x => x.ParagraphID);
                    table.ForeignKey(
                        name: "FK_tblParagraph_tblChapter",
                        column: x => x.ChapID,
                        principalTable: "tblChapter",
                        principalColumn: "ChapterID");
                });

            migrationBuilder.CreateIndex(
                name: "UQ__tblAdmin__536C85E44AFAD200",
                table: "tblAdmin",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tblCategoryOfStory_StoryID",
                table: "tblCategoryOfStory",
                column: "StoryID");

            migrationBuilder.CreateIndex(
                name: "UQ__tblCateg__8AE116288418053F",
                table: "tblCategoryOfStory",
                columns: new[] { "CategoryID", "StoryID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tblChapter_StoryID",
                table: "tblChapter",
                column: "StoryID");

            migrationBuilder.CreateIndex(
                name: "IX_tblChapterComment_ChapterID",
                table: "tblChapterComment",
                column: "ChapterID");

            migrationBuilder.CreateIndex(
                name: "IX_tblChapterComment_UserID",
                table: "tblChapterComment",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_tblComment_StoryID",
                table: "tblComment",
                column: "StoryID");

            migrationBuilder.CreateIndex(
                name: "IX_tblParagraph_ChapID",
                table: "tblParagraph",
                column: "ChapID");

            migrationBuilder.CreateIndex(
                name: "IX_tblStory_AuthorID",
                table: "tblStory",
                column: "AuthorID");

            migrationBuilder.CreateIndex(
                name: "IX_tblUserFollowStory_StoryID",
                table: "tblUserFollowStory",
                column: "StoryID");

            migrationBuilder.CreateIndex(
                name: "UQ__tblUserF__8460E0AF2913C658",
                table: "tblUserFollowStory",
                columns: new[] { "UserID", "StoryID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tblUserLiking_StoryID",
                table: "tblUserLiking",
                column: "StoryID");

            migrationBuilder.CreateIndex(
                name: "IX_tblUserRating_StoryID",
                table: "tblUserRating",
                column: "StoryID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tblAdmin");

            migrationBuilder.DropTable(
                name: "tblCategoryOfStory");

            migrationBuilder.DropTable(
                name: "tblChapterComment");

            migrationBuilder.DropTable(
                name: "tblComment");

            migrationBuilder.DropTable(
                name: "tblParagraph");

            migrationBuilder.DropTable(
                name: "tblUserFollowStory");

            migrationBuilder.DropTable(
                name: "tblUserLiking");

            migrationBuilder.DropTable(
                name: "tblUserRating");

            migrationBuilder.DropTable(
                name: "tblUserReadingProgress");

            migrationBuilder.DropTable(
                name: "tblVocabulary");

            migrationBuilder.DropTable(
                name: "tblCategory");

            migrationBuilder.DropTable(
                name: "tblChapter");

            migrationBuilder.DropTable(
                name: "tblUser");

            migrationBuilder.DropTable(
                name: "tblStory");

            migrationBuilder.DropTable(
                name: "tblAuthor");
        }
    }
}
