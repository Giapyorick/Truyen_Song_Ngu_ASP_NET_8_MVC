using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebTruyenTranh.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK__tblAdmin__719FE4E843CF3D0E",
                table: "tblAdmin");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "tblAdmin");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "tblAdmin");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "tblAdmin");

            migrationBuilder.RenameColumn(
                name: "LikedDate",
                table: "tblUserLiking",
                newName: "LikeDate");

            migrationBuilder.RenameColumn(
                name: "AdminID",
                table: "tblAdmin",
                newName: "AdminId");

            migrationBuilder.RenameIndex(
                name: "UQ__tblAdmin__536C85E44AFAD200",
                table: "tblAdmin",
                newName: "IX_tblAdmin_Username");

            migrationBuilder.AddColumn<string>(
                name: "Lang",
                table: "tblStory",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BlockType",
                table: "tblParagraph",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Chinese",
                table: "tblParagraph",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "French",
                table: "tblParagraph",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Japanese",
                table: "tblParagraph",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "tblAdmin",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "tblAdmin",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLogin",
                table: "tblAdmin",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "tblAdmin",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "tblAdmin",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Viewer");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tblAdmin",
                table: "tblAdmin",
                column: "AdminId");

            migrationBuilder.CreateTable(
                name: "TblCategoryTranslation",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TblCategoryTranslation", x => new { x.CategoryId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_TblCategoryTranslation_tblCategory_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "tblCategory",
                        principalColumn: "CategoryID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TblChapterTranslation",
                columns: table => new
                {
                    ChapterId = table.Column<int>(type: "int", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TblChapterTranslation", x => new { x.ChapterId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_TblChapterTranslation_tblChapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "tblChapter",
                        principalColumn: "ChapterID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tblPosts",
                columns: table => new
                {
                    PostId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoryId = table.Column<int>(type: "int", nullable: true),
                    ChapterId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblPosts", x => x.PostId);
                    table.ForeignKey(
                        name: "FK_tblPosts_tblChapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "tblChapter",
                        principalColumn: "ChapterID");
                    table.ForeignKey(
                        name: "FK_tblPosts_tblStory_StoryId",
                        column: x => x.StoryId,
                        principalTable: "tblStory",
                        principalColumn: "StoryID");
                    table.ForeignKey(
                        name: "FK_tblPosts_tblUser_UserId",
                        column: x => x.UserId,
                        principalTable: "tblUser",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TblStoryTranslation",
                columns: table => new
                {
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TblStoryTranslation", x => new { x.StoryId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_TblStoryTranslation_tblStory_StoryId",
                        column: x => x.StoryId,
                        principalTable: "tblStory",
                        principalColumn: "StoryID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tblUserVocabularies",
                columns: table => new
                {
                    VocabId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    WordOrPhrase = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContextSentence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChapterId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblUserVocabularies", x => x.VocabId);
                });

            migrationBuilder.CreateTable(
                name: "tblPostComments",
                columns: table => new
                {
                    CommentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblPostComments", x => x.CommentId);
                    table.ForeignKey(
                        name: "FK_tblPostComments_tblPostComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "tblPostComments",
                        principalColumn: "CommentId");
                    table.ForeignKey(
                        name: "FK_tblPostComments_tblPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "tblPosts",
                        principalColumn: "PostId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tblPostComments_tblUser_UserId",
                        column: x => x.UserId,
                        principalTable: "tblUser",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tblPostLikes",
                columns: table => new
                {
                    LikeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblPostLikes", x => x.LikeId);
                    table.ForeignKey(
                        name: "FK_tblPostLikes_tblPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "tblPosts",
                        principalColumn: "PostId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tblPostLikes_tblUser_UserId",
                        column: x => x.UserId,
                        principalTable: "tblUser",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tblPostComments_ParentCommentId",
                table: "tblPostComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_tblPostComments_PostId",
                table: "tblPostComments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_tblPostComments_UserId",
                table: "tblPostComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tblPostLikes_PostId",
                table: "tblPostLikes",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_tblPostLikes_UserId",
                table: "tblPostLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tblPosts_ChapterId",
                table: "tblPosts",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_tblPosts_StoryId",
                table: "tblPosts",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_tblPosts_UserId",
                table: "tblPosts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TblCategoryTranslation");

            migrationBuilder.DropTable(
                name: "TblChapterTranslation");

            migrationBuilder.DropTable(
                name: "tblPostComments");

            migrationBuilder.DropTable(
                name: "tblPostLikes");

            migrationBuilder.DropTable(
                name: "TblStoryTranslation");

            migrationBuilder.DropTable(
                name: "tblUserVocabularies");

            migrationBuilder.DropTable(
                name: "tblPosts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tblAdmin",
                table: "tblAdmin");

            migrationBuilder.DropColumn(
                name: "Lang",
                table: "tblStory");

            migrationBuilder.DropColumn(
                name: "BlockType",
                table: "tblParagraph");

            migrationBuilder.DropColumn(
                name: "Chinese",
                table: "tblParagraph");

            migrationBuilder.DropColumn(
                name: "French",
                table: "tblParagraph");

            migrationBuilder.DropColumn(
                name: "Japanese",
                table: "tblParagraph");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "tblAdmin");

            migrationBuilder.DropColumn(
                name: "LastLogin",
                table: "tblAdmin");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "tblAdmin");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "tblAdmin");

            migrationBuilder.RenameColumn(
                name: "LikeDate",
                table: "tblUserLiking",
                newName: "LikedDate");

            migrationBuilder.RenameColumn(
                name: "AdminId",
                table: "tblAdmin",
                newName: "AdminID");

            migrationBuilder.RenameIndex(
                name: "IX_tblAdmin_Username",
                table: "tblAdmin",
                newName: "UQ__tblAdmin__536C85E44AFAD200");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "tblAdmin",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "tblAdmin",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "tblAdmin",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Status",
                table: "tblAdmin",
                type: "bit",
                nullable: true,
                defaultValue: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK__tblAdmin__719FE4E843CF3D0E",
                table: "tblAdmin",
                column: "AdminID");
        }
    }
}
