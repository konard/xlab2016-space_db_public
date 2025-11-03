# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SpaceDb is a hybrid vector database and knowledge graph system for AI memory pools. It combines:
- **RocksDB** (embedded key-value store) for metadata and graph segments
- **Qdrant** (vector database) for semantic search via embeddings
- **PostgreSQL** for relational data (users, roles, tenants, singularities)
- **Redis Stack** for caching

The system enables semantic search over knowledge points with graph-based relationships, multi-layered memory architecture, and multi-tenant isolation.

## Development Setup

### Prerequisites
1. Docker Desktop must be running
2. Visual Studio Enterprise (for building/debugging) or .NET 8.0 SDK
3. PostgreSQL (via Docker - port 54321)
4. Qdrant server (configured in appsettings.json - default: 85.198.90.85:6334)
5. Redis Stack (optional, via Docker)

### Starting Required Services

**MANDATORY - PostgreSQL Database:**
```bash
cd src
up-db.bat          # Starts PostgreSQL on port 54321
```

**Optional - Redis:**
```bash
up-redis.bat       # Starts Redis Stack
redis-cli.bat      # Connect to Redis CLI
```

**Optional - NCANode (port 14579):**
```bash
docker volume create ncanode_cache
docker run --name ncanode -p 14579:14579 -v ncanode_cache:/app/cache -d malikzh/ncanode
```

**Tear down services:**
```bash
down-db.bat        # Stop PostgreSQL
down-redis.bat     # Stop Redis
```

### Building and Running

**Startup project:** `src/SpaceDb/SpaceDb.csproj`

**Build in Visual Studio:**
- Open solution in Visual Studio Enterprise
- Set SpaceDb as startup project
- Build and run (F5)

**Build via CLI:**
```bash
cd src/SpaceDb
dotnet restore
dotnet build
dotnet run
```

**Docker deployment:**
```bash
cd src
up-d.bat           # Build and start API container on ports 444 (HTTPS) and 20101 (HTTP)
down.bat           # Stop API container
```

API will be available at: `http://localhost:20101` or `https://localhost:444`
Swagger UI: `http://localhost:20101/swagger`

### Database Migrations

Migrations use Entity Framework Core:
```bash
cd src/SpaceDb
dotnet ef migrations add MigrationName
dotnet ef database update
```

Connection string is in `appsettings.json` or can be overridden with environment variable:
```bash
# Replace YOUR_PASSWORD with your actual secure password
$env:DB_CONNECTION_STRING="Server=localhost;Port=54321;Database=space_db;User Id=pguser;Password=YOUR_PASSWORD"
```

## Architecture

### Core Concept: Hybrid Storage Model

**Point (Knowledge Node):**
- Metadata → RocksDB (key: `point:{id}`)
- Vector embedding → Qdrant (for semantic search)
- Note: Current implementation does NOT persist `Payload` in RocksDB (line SpaceDbService.cs:53) - this is a known limitation

**Segment (Graph Edge):**
- Stored bidirectionally in RocksDB:
  - Inbound: `segment:in:{fromId}:{toId}`
  - Outbound: `segment:out:{toId}:{fromId}`

**Singularity:**
- Multi-tenant namespace/context
- PostgreSQL entity for metadata
- Used to isolate AI agent memories or user contexts

### Key Components

**SpaceDbService** (`Services/SpaceDbService.cs`):
- Orchestrates RocksDB, Qdrant, and embedding operations
- `AddPointAsync()`: Creates point in both RocksDB and Qdrant
- `SearchAsync()`: Semantic search with filters (layer, dimension, singularityId)
- `AddSegmentAsync()`: Creates graph relationship

**RocksDbService** (`Services/RocksDbService.cs`):
- Low-level key-value operations
- Uses Base64 encoding (performance consideration - see note below)
- Provides JSON helpers: `PutJsonAsync<T>()`, `GetJsonAsync<T>()`

**QdrantService** (`Services/QdrantService.cs`):
- Vector database operations
- `UpsertPointStructsAsync()`: Batch insert vectors
- `SearchWithFilterAsync()`: Vector similarity search with metadata filters
- Uses gRPC client (configured as Scoped - see note below)

**EmbeddingProvider** (`Services/QAiEmbeddingProvider.cs`):
- Wraps external AI API for text→vector conversion
- Configuration in `appsettings.json` under `Providers:QAi`

### Service Registration Pattern

Services are registered in `Helpers/StartupHelper.cs`:
- **Singleton**: RocksDbService (one instance per app lifetime)
- **Scoped**: QdrantService, SpaceDbService, EmbeddingProvider (per HTTP request)
- **Transient**: None

### Data Flow for Point Creation

```
1. HTTP POST /api/v1/points
2. PointsController → SpaceDbService.AddPointAsync()
3. SpaceDbService:
   a. Generate ID (Interlocked.Increment - note: resets on restart)
   b. Save metadata to RocksDB (WITHOUT payload)
   c. Create embedding via EmbeddingProvider (external API call)
   d. Upsert to Qdrant with vector + metadata payload
   e. Optionally create Segment if fromId provided
```

### Critical Architecture Notes

**⚠️ Known Limitations:**

1. **No transactional consistency** between RocksDB and Qdrant - if Qdrant fails, data desynchronizes
2. **Payload is NOT persisted** - original text is lost after embedding creation (SpaceDbService.cs:53)
3. **ID generation not distributed** - `Interlocked.Increment` resets on restart causing collisions
4. **No batch operations** - each point triggers individual embedding API call (major bottleneck)
5. **Base64 encoding overhead** in RocksDB (RocksDbService.cs:39) - increases data size by 33%
6. **Scoped Qdrant client** - creates new gRPC connection per request (should be Singleton)

## API Endpoints

All endpoints require JWT authentication (except health check):

**Core Operations:**
- `POST /api/v1/points` - Add knowledge point (with optional fromId for auto-segment)
- `POST /api/v1/points/search` - Semantic search (query or queryEmbedding)
- `PUT /api/v1/points` - Update point
- `DELETE /api/v1/points/{id}` - Delete point
- `POST /api/v1/segments?fromId=X&toId=Y` - Create graph edge
- `DELETE /api/v1/segments?fromId=X&toId=Y` - Delete edge

**Direct Database Access:**
- `/api/v1/qdrant/*` - Direct Qdrant operations
- `/api/v1/rocksdb/*` - Direct RocksDB operations

**User Management:**
- `/api/v1/users`, `/api/v1/roles`, `/api/v1/tenants`, `/api/v1/singularities`

**Content Parsing (NEW):**
- `POST /api/v1/content/upload` - Upload content with automatic parsing into fragments
- `POST /api/v1/content/upload/batch` - Batch upload multiple content items
- `GET /api/v1/content/parsers` - List available parser types

**Health:**
- `GET /api/v1/health` - Health check endpoint

## Content Parsing System

The content parsing system automatically splits large content into fragments, creating a hierarchical graph structure:

**Architecture:**
```
Resource Point (dimension=0, no embedding)
  ├─> Fragment 1 (dimension=1, with embedding)
  ├─> Fragment 2 (dimension=1, with embedding)
  └─> Fragment N (dimension=1, with embedding)
```

**Available Parsers:**

1. **TextPayloadParser** (`contentType: "text"`):
   - Splits text by double newlines (paragraphs)
   - Handles long paragraphs by splitting at sentence boundaries
   - Configurable min/max paragraph length (default: 50-2000 chars)
   - Normalizes whitespace

2. **JsonPayloadParser** (`contentType: "json"`):
   - Converts JSON into graph nodes
   - Creates fragments for objects, arrays, and string values
   - Maintains hierarchical structure via `ParentKey`
   - Configurable max depth (default: 10)

3. **Auto-detection** (`contentType: "auto"`):
   - Tries each parser in sequence until one succeeds

**Key Benefits:**
- **Batch embedding**: All fragments embedded in single API call (50-100x faster than sequential)
- **Hierarchical search**: Search fragments (dimension=1) and trace back to source (dimension=0)
- **Memory efficiency**: Only fragments have embeddings, resources store metadata only
- **Payload preservation**: Fragment content is stored in `Payload` field

**Example Usage:**
```bash
# Upload text document
POST /api/v1/content/upload
{
  "payload": "Paragraph 1...\n\nParagraph 2...",
  "resourceId": "doc_001.txt",
  "contentType": "text",
  "singularityId": 1
}

# Search fragments
POST /api/v1/points/search
{
  "query": "relevant topic",
  "dimension": 1,  // Search only fragments
  "singularityId": 1
}
```

See `CONTENT_PARSING.md` for detailed documentation.

## Configuration

**appsettings.json** key sections:

```json
{
  "ConnectionStrings": {
    "PostgresConnection": "Server=...;Port=5432;Database=spaces_db;..."
  },
  "Providers": {
    "QAi": {
      "Host": "https://qai.asia",
      "ApiToken": "sk-proj-..."
    },
    "Qdrant": {
      "Host": "85.198.90.85",
      "Port": 6334,
      "CollectionName": "points",
      "VectorSize": 1536,
      "EmbeddingType": "default"
    },
    "RedisUrl": "localhost"
  }
}
```

**Environment variables:**
- `DB_CONNECTION_STRING` - Override PostgreSQL connection
- `ROCKSDB_PATH` - RocksDB data directory (default: `./rocksdb`)

## Project Structure

```
src/
├── SpaceDb/                    # Main ASP.NET Core API
│   ├── Controllers/            # REST API endpoints
│   │   ├── ContentController.cs    # NEW: Content parsing endpoints
│   │   ├── PointsController.cs     # Point management
│   │   └── SegmentsController.cs   # Segment management
│   ├── Services/               # Business logic
│   │   ├── Parsers/                # NEW: Content parsers
│   │   │   ├── PayloadParserBase.cs
│   │   │   ├── TextPayloadParser.cs
│   │   │   └── JsonPayloadParser.cs
│   │   ├── ContentParserService.cs # NEW: Parser orchestration
│   │   ├── SpaceDbService.cs       # Main SpaceDb operations
│   │   ├── QdrantService.cs        # Qdrant vector DB
│   │   └── RocksDbService.cs       # RocksDB key-value
│   ├── Models/                 # DTOs and domain models
│   │   ├── Point.cs
│   │   ├── Segment.cs
│   │   ├── ContentFragment.cs      # NEW: Parsed fragment
│   │   └── ParsedResource.cs       # NEW: Parse result
│   ├── Data/SpaceDb/           # EF Core entities and DbContext
│   └── Helpers/                # Startup configuration
├── Libs/                       # Shared libraries
│   ├── Api.AspNetCore/         # API infrastructure (filters, auth)
│   ├── Data.Repository/        # Repository pattern base classes
│   ├── Data.Mapping/           # Mapping abstractions
│   ├── Core.Workflow/          # Workflow engine
│   ├── AI/                     # AI abstractions (AIEmbedding, etc.)
│   └── AI.Client/              # AI API client (QAiClient)
└── docker-compose*.yml         # Container orchestration
```

## Coding Conventions

Follow .NET conventions per `.cursor/rules/dotnet-rule.mdc`:
- PascalCase for types, methods, public members
- camelCase for private fields, local variables
- Prefix interfaces with "I"
- Use async/await for all I/O operations
- XML documentation comments for public APIs (Swagger integration)
- Dependency Injection via constructor injection

## Performance Considerations

**For bulk operations:**
- Current implementation processes points sequentially - each requires embedding API call (~200-500ms)
- Consider batching: `CreateEmbeddingsAsync()` exists but is not used in main flow
- RocksDB WriteBatch not implemented - each point is individual write

**Optimization opportunities:**
1. Batch embedding creation (50-100x speedup)
2. Use RocksDB WriteBatch (5-10x speedup)
3. Change QdrantService to Singleton (reduce connection overhead)
4. Remove Base64 encoding in RocksDB (33% size reduction)
5. Persist payload in RocksDB for data recovery

**Current throughput estimate:**
- Sequential: ~3-5 points/second (limited by embedding API)
- With optimizations: ~500-1000 points/second

## Testing

Tests should be run in Visual Studio Enterprise (per Cursor rules).

**Test frameworks:**
- xUnit, NUnit, or MSTest
- Moq or NSubstitute for mocking

**Testing considerations:**
- Mock external dependencies (QAiClient, Qdrant, RocksDB)
- Use in-memory SQLite for EF Core tests (SpaceDbContext supports `IsInMemoryDb()`)

## Use Cases

**AI Memory Pool:**
- Layer 0: Raw facts/conversations
- Layer 1: Extracted knowledge
- Layer 2: Concepts and generalizations
- Layer 3: Meta-patterns

**Dimensions:** Domain separation (e.g., dimension=1 for tech, dimension=2 for personal)

**Weight:** Importance scoring for memory prioritization

**Singularities:** Isolate memories per user/agent/context

**Graph traversal:** Navigate knowledge via segments (though advanced graph queries not implemented)

## Redis Integration

Optional Redis caching via RedisDictionary (injected):
```csharp
// Write
redisDictionary.Save(key, value);
redisDictionary.SaveCorrelation(key1, key2);

// Read
var value = redisDictionary.Get(key);
var correlated = redisDictionary.GetCorrelation(key);
```

---

Issue to solve: https://github.com/xlab2016/space_db_public/issues/1
Your prepared branch: issue-1-691bfacc41a2
Your prepared working directory: /tmp/gh-issue-solver-1762174098911
Your forked repository: konard/space_db_public
Original repository (upstream): xlab2016/space_db_public

Proceed.