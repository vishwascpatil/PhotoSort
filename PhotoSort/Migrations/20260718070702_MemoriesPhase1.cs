using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoSort.Migrations
{
    /// <inheritdoc />
    public partial class MemoriesPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemoryCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "candidate"),
                    MemoryTypeKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PhotoIds = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoryGenerationHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MemoryTypeKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CandidatesIn = table.Column<int>(type: "INTEGER", nullable: false),
                    CandidatesOut = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryGenerationHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoryPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MaxDailyMemories = table.Column<int>(type: "INTEGER", nullable: false),
                    PreferredTypes = table.Column<string>(type: "TEXT", nullable: true),
                    ExcludedTypes = table.Column<string>(type: "TEXT", nullable: true),
                    MinScore = table.Column<double>(type: "REAL", nullable: false),
                    IncludeScreenshots = table.Column<bool>(type: "INTEGER", nullable: false),
                    WeekdayMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "balanced"),
                    MusicPreference = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "none"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemorySchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScheduleDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScheduleType = table.Column<string>(type: "TEXT", nullable: false),
                    Recurrence = table.Column<string>(type: "TEXT", nullable: true),
                    LastShownAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemorySchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoryTypeFamilies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryTypeFamilies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhotoInteractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoInteractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhotoSignals",
                columns: table => new
                {
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Signals = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    ExtractedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoSignals", x => x.PhotoId);
                });

            migrationBuilder.CreateTable(
                name: "UnknownFaceClusters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClusterLabel = table.Column<string>(type: "TEXT", nullable: true),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PhotoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CentroidEmbedding = table.Column<byte[]>(type: "BLOB", nullable: true),
                    IsNamed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnknownFaceClusters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoryTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Tone = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    MinPhotoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxPhotoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultCoverStrategy = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "single"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetIntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonalMonths = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryTypes_MemoryTypeFamilies_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "MemoryTypeFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MemoryTypeEntityId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Subtitle = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    StorySummary = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CoverPhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    CoverThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    DateStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LocationSummary = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    PeopleSummary = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    IsGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastShownAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ShowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Dismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    SnoozedUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Memories_MemoryTypes_MemoryTypeEntityId",
                        column: x => x.MemoryTypeEntityId,
                        principalTable: "MemoryTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MemoryStatistics",
                columns: table => new
                {
                    MemoryTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalGenerated = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalViewed = table.Column<int>(type: "INTEGER", nullable: false),
                    AvgScore = table.Column<double>(type: "REAL", nullable: false),
                    LastGeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserViewCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UserDwellSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    UserFavoriteCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryStatistics", x => x.MemoryTypeId);
                    table.ForeignKey(
                        name: "FK_MemoryStatistics_MemoryTypes_MemoryTypeId",
                        column: x => x.MemoryTypeId,
                        principalTable: "MemoryTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemoryFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Feedback = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryFeedback_Memories_MemoryId",
                        column: x => x.MemoryId,
                        principalTable: "Memories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemoryItems",
                columns: table => new
                {
                    MemoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "supporting"),
                    QualityScore = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryItems", x => new { x.MemoryId, x.PhotoId });
                    table.ForeignKey(
                        name: "FK_MemoryItems_Memories_MemoryId",
                        column: x => x.MemoryId,
                        principalTable: "Memories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemoryPhotos",
                columns: table => new
                {
                    MemoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Supporting")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryPhotos", x => new { x.MemoryId, x.PhotoId });
                    table.ForeignKey(
                        name: "FK_MemoryPhotos_Memories_MemoryId",
                        column: x => x.MemoryId,
                        principalTable: "Memories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemoryScores",
                columns: table => new
                {
                    MemoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sharpness = table.Column<double>(type: "REAL", nullable: true),
                    Brightness = table.Column<double>(type: "REAL", nullable: true),
                    Noise = table.Column<double>(type: "REAL", nullable: true),
                    Composition = table.Column<double>(type: "REAL", nullable: true),
                    FaceCount = table.Column<int>(type: "INTEGER", nullable: true),
                    SmileScore = table.Column<double>(type: "REAL", nullable: true),
                    EyeOpenness = table.Column<double>(type: "REAL", nullable: true),
                    QualityScore = table.Column<double>(type: "REAL", nullable: true),
                    Importance = table.Column<double>(type: "REAL", nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryScores", x => new { x.MemoryId, x.PhotoId });
                    table.ForeignKey(
                        name: "FK_MemoryScores_Memories_MemoryId",
                        column: x => x.MemoryId,
                        principalTable: "Memories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Memories_DateEnd",
                table: "Memories",
                column: "DateEnd");

            migrationBuilder.CreateIndex(
                name: "IX_Memories_DateStart",
                table: "Memories",
                column: "DateStart");

            migrationBuilder.CreateIndex(
                name: "IX_Memories_GeneratedAt",
                table: "Memories",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Memories_IsArchived_Dismissed",
                table: "Memories",
                columns: new[] { "IsArchived", "Dismissed" });

            migrationBuilder.CreateIndex(
                name: "IX_Memories_IsGenerated",
                table: "Memories",
                column: "IsGenerated");

            migrationBuilder.CreateIndex(
                name: "IX_Memories_LastShownAt",
                table: "Memories",
                column: "LastShownAt");

            migrationBuilder.CreateIndex(
                name: "IX_Memories_MemoryTypeEntityId",
                table: "Memories",
                column: "MemoryTypeEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Memories_Score",
                table: "Memories",
                column: "Score");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryCache_ExpiresAt",
                table: "MemoryCache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryCache_Type",
                table: "MemoryCache",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryFeedback_CreatedAt",
                table: "MemoryFeedback",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryFeedback_MemoryId",
                table: "MemoryFeedback",
                column: "MemoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryGenerationHistory_RunId",
                table: "MemoryGenerationHistory",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryGenerationHistory_RunId_Stage",
                table: "MemoryGenerationHistory",
                columns: new[] { "RunId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryGenerationHistory_StartedAt",
                table: "MemoryGenerationHistory",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryItems_MemoryId",
                table: "MemoryItems",
                column: "MemoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryItems_MemoryId_SortOrder",
                table: "MemoryItems",
                columns: new[] { "MemoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryItems_PhotoId",
                table: "MemoryItems",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryPhotos_MemoryId_SortOrder",
                table: "MemoryPhotos",
                columns: new[] { "MemoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryPhotos_PhotoId",
                table: "MemoryPhotos",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryScores_CalculatedAt",
                table: "MemoryScores",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryScores_PhotoId",
                table: "MemoryScores",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryStatistics_UpdatedAt",
                table: "MemoryStatistics",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryTypeFamilies_Name",
                table: "MemoryTypeFamilies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoryTypeFamilies_SortOrder",
                table: "MemoryTypeFamilies",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryTypes_FamilyId",
                table: "MemoryTypes",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryTypes_IsActive",
                table: "MemoryTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryTypes_Key",
                table: "MemoryTypes",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoryTypes_SortOrder",
                table: "MemoryTypes",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoryCache");

            migrationBuilder.DropTable(
                name: "MemoryFeedback");

            migrationBuilder.DropTable(
                name: "MemoryGenerationHistory");

            migrationBuilder.DropTable(
                name: "MemoryItems");

            migrationBuilder.DropTable(
                name: "MemoryPhotos");

            migrationBuilder.DropTable(
                name: "MemoryPreferences");

            migrationBuilder.DropTable(
                name: "MemorySchedules");

            migrationBuilder.DropTable(
                name: "MemoryScores");

            migrationBuilder.DropTable(
                name: "MemoryStatistics");

            migrationBuilder.DropTable(
                name: "PhotoInteractions");

            migrationBuilder.DropTable(
                name: "PhotoSignals");

            migrationBuilder.DropTable(
                name: "UnknownFaceClusters");

            migrationBuilder.DropTable(
                name: "Memories");

            migrationBuilder.DropTable(
                name: "MemoryTypes");

            migrationBuilder.DropTable(
                name: "MemoryTypeFamilies");
        }
    }
}
