using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoSort.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoThumbnailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FolderPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    AddedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastScanDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ThumbnailPhotoId = table.Column<int>(type: "INTEGER", nullable: true),
                    FaceCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    PhotoCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Places",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Places", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartLatitude = table.Column<double>(type: "REAL", nullable: true),
                    StartLongitude = table.Column<double>(type: "REAL", nullable: true),
                    EndLatitude = table.Column<double>(type: "REAL", nullable: true),
                    EndLongitude = table.Column<double>(type: "REAL", nullable: true),
                    TotalDistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    PhotoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    CoverPhotoPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CoverPhotoId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Photos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DateTaken = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ThumbnailSmallPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ThumbnailMediumPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ThumbnailGeneratedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModifiedDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FolderId = table.Column<int>(type: "INTEGER", nullable: false),
                    CameraMake = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CameraModel = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Orientation = table.Column<int>(type: "INTEGER", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    Duration = table.Column<double>(type: "REAL", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataExtractedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    HashCalculatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DuplicateGroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    MediaCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassificationConfidence = table.Column<double>(type: "REAL", nullable: false),
                    ClassificationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PerceptualHash = table.Column<ulong>(type: "INTEGER", nullable: true),
                    PerceptualHashDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SimilarPhotoGroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    VideoThumbnailSmallPath = table.Column<string>(type: "TEXT", nullable: true),
                    VideoThumbnailMediumPath = table.Column<string>(type: "TEXT", nullable: true),
                    VideoThumbnailLargePath = table.Column<string>(type: "TEXT", nullable: true),
                    VideoThumbnailTimestamp = table.Column<double>(type: "REAL", nullable: true),
                    VideoThumbnailScore = table.Column<double>(type: "REAL", nullable: true),
                    VideoThumbnailVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoThumbnailDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviewStripGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreviewStripVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviewFrameCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Photos_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripPlaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TripId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstVisitDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastVisitDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VisitCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripPlaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripPlaces_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripPlaces_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Faces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoundingBoxX = table.Column<double>(type: "REAL", nullable: false),
                    BoundingBoxY = table.Column<double>(type: "REAL", nullable: false),
                    BoundingBoxWidth = table.Column<double>(type: "REAL", nullable: false),
                    BoundingBoxHeight = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkX1 = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkY1 = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkX2 = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkY2 = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkX3 = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkY3 = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkX4 = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkY4 = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkX5 = table.Column<double>(type: "REAL", nullable: false),
                    LandmarkY5 = table.Column<double>(type: "REAL", nullable: false),
                    FaceAngle = table.Column<double>(type: "REAL", nullable: false),
                    FaceSize = table.Column<double>(type: "REAL", nullable: false),
                    DetectionModelVersion = table.Column<string>(type: "TEXT", nullable: true),
                    IsIgnored = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    RecognitionState = table.Column<int>(type: "INTEGER", nullable: false),
                    RecognitionConfidence = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    LastRecognitionDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Faces_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoPlaces",
                columns: table => new
                {
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoPlaces", x => new { x.PhotoId, x.PlaceId });
                    table.ForeignKey(
                        name: "FK_PhotoPlaces_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhotoPlaces_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoTags",
                columns: table => new
                {
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoTags", x => new { x.PhotoId, x.TagId });
                    table.ForeignKey(
                        name: "FK_PhotoTags_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhotoTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TripId = table.Column<int>(type: "INTEGER", nullable: false),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateTaken = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripPhotos_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripPhotos_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FaceEmbeddings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Embedding = table.Column<string>(type: "TEXT", nullable: false),
                    ModelVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingDimension = table.Column<int>(type: "INTEGER", nullable: false),
                    IsNormalized = table.Column<bool>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaceEmbeddings_Faces_FaceId",
                        column: x => x.FaceId,
                        principalTable: "Faces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonFaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    FaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonFaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonFaces_Faces_FaceId",
                        column: x => x.FaceId,
                        principalTable: "Faces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonFaces_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaceEmbeddings_FaceId",
                table: "FaceEmbeddings",
                column: "FaceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faces_CreatedDate",
                table: "Faces",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Faces_DetectionModelVersion",
                table: "Faces",
                column: "DetectionModelVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Faces_FaceSize",
                table: "Faces",
                column: "FaceSize");

            migrationBuilder.CreateIndex(
                name: "IX_Faces_IsIgnored",
                table: "Faces",
                column: "IsIgnored");

            migrationBuilder.CreateIndex(
                name: "IX_Faces_LastRecognitionDate",
                table: "Faces",
                column: "LastRecognitionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Faces_PhotoId",
                table: "Faces",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_Faces_RecognitionState",
                table: "Faces",
                column: "RecognitionState");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_AddedDate",
                table: "Folders",
                column: "AddedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_FolderPath",
                table: "Folders",
                column: "FolderPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_LastScanDate",
                table: "Folders",
                column: "LastScanDate");

            migrationBuilder.CreateIndex(
                name: "IX_People_CreatedDate",
                table: "People",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_People_FaceCount",
                table: "People",
                column: "FaceCount");

            migrationBuilder.CreateIndex(
                name: "IX_People_LastSeenDate",
                table: "People",
                column: "LastSeenDate");

            migrationBuilder.CreateIndex(
                name: "IX_People_Name",
                table: "People",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_People_PhotoCount",
                table: "People",
                column: "PhotoCount");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFaces_AssignedDate",
                table: "PersonFaces",
                column: "AssignedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFaces_FaceId",
                table: "PersonFaces",
                column: "FaceId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFaces_PersonId",
                table: "PersonFaces",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFaces_PersonId_FaceId",
                table: "PersonFaces",
                columns: new[] { "PersonId", "FaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotoPlaces_PlaceId",
                table: "PhotoPlaces",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_ContentHash",
                table: "Photos",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_DateTaken",
                table: "Photos",
                column: "DateTaken");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_DuplicateGroupId",
                table: "Photos",
                column: "DuplicateGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_FilePath",
                table: "Photos",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Photos_FolderId",
                table: "Photos",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_FolderId_State",
                table: "Photos",
                columns: new[] { "FolderId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Photos_FolderId_State_ModifiedDateUtc",
                table: "Photos",
                columns: new[] { "FolderId", "State", "ModifiedDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Photos_IsFavorite",
                table: "Photos",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_Latitude",
                table: "Photos",
                column: "Latitude");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_Latitude_Longitude",
                table: "Photos",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Photos_Longitude",
                table: "Photos",
                column: "Longitude");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_MediaCategory",
                table: "Photos",
                column: "MediaCategory");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_PerceptualHash",
                table: "Photos",
                column: "PerceptualHash");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_SimilarPhotoGroupId",
                table: "Photos",
                column: "SimilarPhotoGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_State",
                table: "Photos",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_ThumbnailGeneratedDate",
                table: "Photos",
                column: "ThumbnailGeneratedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoTags_TagId",
                table: "PhotoTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Places_Latitude",
                table: "Places",
                column: "Latitude");

            migrationBuilder.CreateIndex(
                name: "IX_Places_Longitude",
                table: "Places",
                column: "Longitude");

            migrationBuilder.CreateIndex(
                name: "IX_Places_Name",
                table: "Places",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripPhotos_DateTaken",
                table: "TripPhotos",
                column: "DateTaken");

            migrationBuilder.CreateIndex(
                name: "IX_TripPhotos_PhotoId",
                table: "TripPhotos",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPhotos_TripId",
                table: "TripPhotos",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPhotos_TripId_PhotoId",
                table: "TripPhotos",
                columns: new[] { "TripId", "PhotoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripPlaces_PlaceId",
                table: "TripPlaces",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPlaces_TripId",
                table: "TripPlaces",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPlaces_TripId_PlaceId",
                table: "TripPlaces",
                columns: new[] { "TripId", "PlaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_EndDate",
                table: "Trips",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_IsFavorite",
                table: "Trips",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_Name",
                table: "Trips",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_StartDate",
                table: "Trips",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_StartDate_EndDate",
                table: "Trips",
                columns: new[] { "StartDate", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaceEmbeddings");

            migrationBuilder.DropTable(
                name: "PersonFaces");

            migrationBuilder.DropTable(
                name: "PhotoPlaces");

            migrationBuilder.DropTable(
                name: "PhotoTags");

            migrationBuilder.DropTable(
                name: "TripPhotos");

            migrationBuilder.DropTable(
                name: "TripPlaces");

            migrationBuilder.DropTable(
                name: "Faces");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Places");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Photos");

            migrationBuilder.DropTable(
                name: "Folders");
        }
    }
}
