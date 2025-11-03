# SpaceDb

**A Hybrid Vector Database and Knowledge Graph System for AI Memory Pools**

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg)](https://www.docker.com/)

---

## Overview

SpaceDb is a sophisticated hybrid database system that combines the strengths of **vector databases**, **key-value stores**, and **relational databases** to create a powerful foundation for AI memory and knowledge management systems. It enables semantic search over knowledge points while maintaining graph-based relationships and multi-tenant isolation.

### Key Features

- **Hybrid Storage Architecture**: Combines RocksDB (key-value), Qdrant (vector), PostgreSQL (relational), and Redis (caching)
- **Semantic Search**: Vector embeddings powered by Qdrant for intelligent similarity search
- **Knowledge Graph**: Bidirectional graph relationships with rich metadata
- **Content Parsing**: Automatic splitting of documents and JSON into searchable fragments
- **Multi-Tenant Isolation**: Singularities for per-user/per-agent memory isolation
- **Hierarchical Memory**: Layer-based organization for temporal knowledge management
- **Batch Operations**: Optimized batch embedding for 50-100x performance improvements
- **RESTful API**: Comprehensive API with Swagger documentation
- **Enterprise-Ready**: JWT authentication, role-based access control, structured logging

---

## Table of Contents

- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Installation](#installation)
- [Configuration](#configuration)
- [API Documentation](#api-documentation)
- [Use Cases](#use-cases)
- [Content Parsing System](#content-parsing-system)
- [Development](#development)
- [Performance](#performance)
- [Contributing](#contributing)
- [License](#license)

---

## Architecture

### Hybrid Storage Model

SpaceDb uses three complementary storage systems to maximize performance and flexibility:

```
┌─────────────────────────────────────────────────────────┐
│                      SpaceDb API                        │
│                    (ASP.NET Core 8.0)                   │
└───────────────┬─────────────┬────────────┬──────────────┘
                │             │            │
        ┌───────▼──────┐  ┌──▼─────┐  ┌──▼──────────┐
        │   RocksDB    │  │ Qdrant │  │ PostgreSQL  │
        │  (Metadata)  │  │(Vectors)│  │   (Users)   │
        └──────────────┘  └────────┘  └─────────────┘
        Key-Value Store   Vector DB    Relational DB
```

| Component | Purpose | Technology |
|-----------|---------|------------|
| **RocksDB** | Point metadata, graph segments | Embedded key-value store |
| **Qdrant** | Vector embeddings, semantic search | Vector database (gRPC) |
| **PostgreSQL** | Users, roles, tenants, singularities | Relational database |
| **Redis** | Optional caching layer | Redis Stack |

### Core Data Model

**Point (Knowledge Node):**
```csharp
{
  "id": 1001,
  "layer": 0,           // Temporal hierarchy (0=raw, 1=extracted, 2=concepts, 3=meta)
  "dimension": 1,       // Domain separation (0=resource, 1=fragment, 2=concepts)
  "weight": 0.8,        // Importance score
  "singularityId": 1,   // Multi-tenant isolation
  "userId": 123,        // User attribution
  "payload": "text"     // Content (stored in Qdrant only)
}
```

**Segment (Graph Edge):**
```csharp
{
  "id": 2001,
  "fromId": 1001,       // Source point
  "toId": 1002,         // Target point
  "weight": 0.9,
  "layer": 0,
  "dimension": 1
}
```

**Hierarchical Content Structure:**
```
Resource Point (dimension=0, no embedding)
  ├─> Fragment 1 (dimension=1, with embedding) ─┐
  ├─> Fragment 2 (dimension=1, with embedding) ─┤
  └─> Fragment N (dimension=1, with embedding) ─┴─> Searchable
```

---

## Quick Start

### Prerequisites

- **Docker Desktop** (running)
- **.NET 8.0 SDK** or **Visual Studio Enterprise**
- **PostgreSQL** (via Docker)
- **Qdrant Server** (configured in appsettings.json)

### 1. Start PostgreSQL Database

```bash
cd src
./up-db.bat  # Windows
# or
docker-compose -f docker-compose-db.yml up -d  # Linux/Mac
```

This starts PostgreSQL on port `54321` with credentials:
- **Host**: localhost:54321
- **Database**: space_db
- **User**: pguser
- **Password**: pguserpass

### 2. Configure Qdrant Connection

Edit `src/SpaceDb/appsettings.json`:

```json
{
  "Providers": {
    "Qdrant": {
      "Host": "85.198.90.85",
      "Port": 6334,
      "CollectionName": "points",
      "VectorSize": 1536
    }
  }
}
```

### 3. Run SpaceDb API

**Option A: Visual Studio**
```
1. Open src/SpaceDb.sln
2. Set SpaceDb as startup project
3. Press F5
```

**Option B: Command Line**
```bash
cd src/SpaceDb
dotnet restore
dotnet build
dotnet run
```

**Option C: Docker**
```bash
cd src
./up-d.bat  # Starts API on ports 444 (HTTPS) and 20101 (HTTP)
```

### 4. Access API

- **Swagger UI**: http://localhost:20101/swagger
- **API Base URL**: http://localhost:20101/api/v1
- **Health Check**: http://localhost:20101/api/v1/health

---

## Installation

### Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/xlab2016/space_db.git
   cd space_db
   ```

2. **Start required services**
   ```bash
   cd src
   ./up-db.bat          # PostgreSQL
   ./up-redis.bat       # Redis (optional)
   ```

3. **Configure environment variables**
   ```bash
   # Copy the example file and set your own secure password
   cd src
   cp .env.example .env
   # Edit .env and set POSTGRES_PASSWORD to a secure value

   # Or export directly (replace YOUR_PASSWORD with a secure password):
   export DB_CONNECTION_STRING="Server=localhost;Port=54321;Database=space_db;User Id=pguser;Password=YOUR_PASSWORD"
   export ROCKSDB_PATH="./rocksdb"
   ```

4. **Run database migrations**
   ```bash
   cd src/SpaceDb
   dotnet ef database update
   ```
   > Migrations are auto-applied on startup by default

5. **Build and run**
   ```bash
   dotnet run
   ```

### Docker Deployment

```bash
cd src
./up-d.bat  # Windows
# or
docker-compose --file docker-compose.yml up -d  # Linux/Mac
```

API will be available at:
- HTTP: `http://localhost:20101`
- HTTPS: `https://localhost:444`

---

## Configuration

### appsettings.json

Key configuration sections:

```json
{
  "ConnectionStrings": {
    "PostgresConnection": "Server=localhost;Port=54321;Database=space_db;..."
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
  },
  "tokenManagement": {
    "secret": "your-secret-key",
    "issuer": "localhost",
    "audience": "account",
    "refreshExpiration": "60"
  }
}
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DB_CONNECTION_STRING` | PostgreSQL connection string | From appsettings.json |
| `ROCKSDB_PATH` | RocksDB data directory | `./rocksdb` |

---

## API Documentation

### Authentication

All endpoints (except `/api/v1/health`) require JWT Bearer authentication:

```bash
Authorization: Bearer <your-jwt-token>
```

### Core Endpoints

#### Points API

**Create Point**
```bash
POST /api/v1/points
Content-Type: application/json
Authorization: Bearer <token>

{
  "layer": 0,
  "dimension": 1,
  "weight": 0.8,
  "singularityId": 1,
  "userId": 123,
  "payload": "This is a knowledge point about AI systems.",
  "fromId": null  // Optional: auto-creates segment if provided
}
```

**Search Points (Semantic)**
```bash
POST /api/v1/points/search
Content-Type: application/json
Authorization: Bearer <token>

{
  "query": "AI systems and knowledge graphs",
  "singularityId": 1,
  "dimension": 1,
  "limit": 10,
  "scoreThreshold": 0.7
}
```

**Update Point**
```bash
PUT /api/v1/points
Content-Type: application/json
Authorization: Bearer <token>

{
  "id": 1001,
  "layer": 1,
  "dimension": 1,
  "weight": 0.9,
  "payload": "Updated content"
}
```

**Delete Point**
```bash
DELETE /api/v1/points/1001
Authorization: Bearer <token>
```

#### Segments API

**Create Segment (Graph Edge)**
```bash
POST /api/v1/segments?fromId=1001&toId=1002
Authorization: Bearer <token>
```

**Delete Segment**
```bash
DELETE /api/v1/segments?fromId=1001&toId=1002
Authorization: Bearer <token>
```

#### Content Parsing API

**Upload Content for Auto-Parsing**
```bash
POST /api/v1/content/upload
Content-Type: application/json
Authorization: Bearer <token>

{
  "payload": "First paragraph...\n\nSecond paragraph...\n\nThird paragraph...",
  "resourceId": "document_001.txt",
  "contentType": "text",  // "text", "json", or "auto"
  "singularityId": 1,
  "userId": 123,
  "metadata": {
    "source": "manual_upload",
    "author": "John Doe"
  }
}
```

**Response:**
```json
{
  "data": {
    "resourcePointId": 1001,
    "fragmentPointIds": [1002, 1003, 1004],
    "segmentIds": [],
    "parserType": "text",
    "totalFragments": 3
  },
  "message": "Successfully created resource 1001 with 3 fragments"
}
```

**List Available Parsers**
```bash
GET /api/v1/content/parsers
Authorization: Bearer <token>
```

### Additional Endpoints

- **User Management**: `/api/v1/users`, `/api/v1/roles`, `/api/v1/userroles`
- **Multi-Tenancy**: `/api/v1/tenants`, `/api/v1/singularities`
- **Direct Database Access**: `/api/v1/rocksdb/*`, `/api/v1/qdrant/*`
- **Health Check**: `/api/v1/health` (no authentication required)

### Swagger Documentation

Interactive API documentation is available at:
```
http://localhost:20101/swagger
```

---

## Use Cases

### 1. AI Memory Pool

Create multi-layered memory for AI agents:

```
Layer 0: Raw conversations and facts
Layer 1: Extracted knowledge and insights
Layer 2: Concepts and generalizations
Layer 3: Meta-patterns and strategies
```

Each layer can be searched independently, and segments connect related knowledge across layers.

### 2. Document Management System

```bash
# Upload document
POST /api/v1/content/upload
{
  "payload": "Long document with multiple sections...",
  "resourceId": "technical_report_2025.pdf",
  "contentType": "text"
}

# Search within document fragments
POST /api/v1/points/search
{
  "query": "performance metrics",
  "dimension": 1,  // Search fragments only
  "limit": 5
}
```

### 3. Knowledge Graph Construction

Build interconnected knowledge bases:

```bash
# Create concept nodes
POST /api/v1/points
{
  "payload": "Machine learning is a subset of artificial intelligence",
  "dimension": 2  // Concept dimension
}

# Create relationships
POST /api/v1/segments?fromId=<ML_id>&toId=<AI_id>
```

### 4. Configuration Management

Store and search configuration files:

```bash
POST /api/v1/content/upload
{
  "payload": "{\"database\":{\"host\":\"localhost\",\"port\":5432}}",
  "resourceId": "production_config.json",
  "contentType": "json"
}
```

The JSON parser will automatically create searchable fragments for nested objects and values.

---

## Content Parsing System

SpaceDb includes a powerful content parsing system that automatically splits large documents into searchable fragments.

### Features

- **Text Parser**: Splits documents by paragraphs (double newlines)
- **JSON Parser**: Converts JSON into hierarchical graph nodes
- **Auto-Detection**: Automatically determines content type
- **Batch Embedding**: Embeds all fragments in a single API call (50-100x faster)
- **Hierarchical Structure**: Resource points (dimension=0) linked to fragment points (dimension=1)

### Text Parsing Example

**Input:**
```
This is the first paragraph with some important information.

This is the second paragraph containing different details.

This is the third and final paragraph.
```

**Output:**
- 1 Resource point (dimension=0, no embedding)
- 3 Fragment points (dimension=1, with embeddings)
- 3 Segments connecting resource to each fragment

### JSON Parsing Example

**Input:**
```json
{
  "user": {
    "name": "Alice",
    "bio": "Software engineer with passion for AI"
  },
  "settings": {
    "theme": "dark"
  }
}
```

**Output:**
- 1 Resource point (dimension=0)
- Multiple Fragment points for:
  - Root object summary
  - Nested objects (`user`, `settings`)
  - String values longer than 20 characters (`bio`)
- Segments maintaining parent-child relationships

### Configuration

**Text Parser:**
```csharp
minParagraphLength: 50    // Skip paragraphs shorter than this
maxParagraphLength: 2000  // Split long paragraphs by sentences
```

**JSON Parser:**
```csharp
maxDepth: 10              // Maximum nesting depth
includeArrays: true       // Parse array elements
```

For detailed documentation, see [CONTENT_PARSING.md](CONTENT_PARSING.md).

---

## Development

### Project Structure

```
src/
├── SpaceDb/                    # Main ASP.NET Core API
│   ├── Controllers/            # REST API endpoints
│   ├── Services/               # Business logic
│   │   ├── SpaceDbService.cs       # Main orchestrator
│   │   ├── RocksDbService.cs       # Key-value operations
│   │   ├── QdrantService.cs        # Vector database
│   │   ├── ContentParserService.cs # Content parsing
│   │   └── Parsers/                # Text/JSON parsers
│   ├── Models/                 # DTOs and domain models
│   ├── Data/SpaceDb/           # EF Core entities
│   └── Helpers/                # Startup configuration
├── Libs/                       # Shared libraries
│   ├── AI/                     # AI abstractions
│   ├── AI.Client/              # AI API client
│   ├── Api.AspNetCore/         # API infrastructure
│   ├── Data.Repository/        # Repository pattern
│   ├── Data.Mapping/           # Mapping utilities
│   └── Core.Workflow/          # Workflow engine
└── docker-compose*.yml         # Container orchestration
```

### Technology Stack

- **Framework**: ASP.NET Core 8.0
- **Language**: C# 12 with nullable reference types
- **Databases**: RocksDB 6.2.2, Qdrant 1.7.0, PostgreSQL (Npgsql 6.0)
- **Caching**: Redis Stack 2.7.33
- **Authentication**: JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer 8.0.1)
- **ORM**: Entity Framework Core 6.0, Dapper 2.0.123
- **Logging**: Serilog with Elasticsearch sink
- **API Docs**: Swashbuckle (Swagger) 6.2.3

### Building from Source

```bash
cd src/SpaceDb
dotnet restore
dotnet build
dotnet test  # Run tests
```

### Database Migrations

```bash
# Create new migration
dotnet ef migrations add MigrationName --project src/SpaceDb

# Apply migrations
dotnet ef database update --project src/SpaceDb
```

### Running Tests

```bash
cd src
dotnet test
```

Test frameworks supported: xUnit, NUnit, MSTest

### Coding Conventions

- PascalCase for types, methods, public members
- camelCase for private fields, local variables
- Prefix interfaces with "I"
- Use async/await for all I/O operations
- XML documentation comments for public APIs

---

## Performance

### Current Performance

- **Sequential Point Creation**: ~3-5 points/second (limited by embedding API)
- **Batch Content Parsing**: ~500-1000 points/second (with optimizations)
- **Search Latency**: <100ms for typical queries

### Optimization Strategies

The content parsing system demonstrates significant performance improvements:

```
Sequential: 1000 fragments × 300ms = 5 minutes
Batch:      10 batches × 300ms = 3 seconds (100x faster!)
```

### Performance Considerations

**Storage Overhead:**
- Base64 encoding in RocksDB adds ~33% size overhead
- Consider binary storage for production deployments

**Service Lifetimes:**
- RocksDbService: Singleton (one instance per app)
- QdrantService: Scoped (per HTTP request)
- Consider making QdrantService Singleton for gRPC connection reuse

**Batch Operations:**
- Use content parsing endpoints for bulk document uploads
- Batch embedding creation is 50-100x faster than sequential

---

## Contributing

We welcome contributions! Please follow these guidelines:

1. **Fork the repository** and create a feature branch
2. **Follow coding conventions** (see Development section)
3. **Write tests** for new features
4. **Update documentation** (README, CLAUDE.md, API docs)
5. **Submit a pull request** with clear description

### Development Guidelines

- Use dependency injection for all services
- Mock external dependencies in tests
- Add XML documentation comments for public APIs
- Follow existing patterns in the codebase
- Test with multiple tenants/singularities

For detailed development instructions, see [CLAUDE.md](CLAUDE.md).

---

## License

The license of work is not defined yet and currently is No-License with all copyright protections.

---

## Credits

### Built With

- [RocksDB](https://rocksdb.org/) - Embedded key-value store
- [Qdrant](https://qdrant.tech/) - Vector database for semantic search
- [PostgreSQL](https://www.postgresql.org/) - Relational database
- [Redis](https://redis.io/) - In-memory caching
- [ASP.NET Core](https://dotnet.microsoft.com/) - Web framework

### Acknowledgments

SpaceDb is designed for general-purpose AI memory systems, enabling intelligent agents to store, retrieve, and organize knowledge with semantic understanding and graph-based relationships.

---

## Support

- **Issues**: [GitHub Issues](https://github.com/xlab2016/space_db/issues)
- **Documentation**: See [CLAUDE.md](CLAUDE.md) and [CONTENT_PARSING.md](CONTENT_PARSING.md)
- **API Docs**: Available at `/swagger` when running

---

## Roadmap

- [ ] Distributed ID generation (Snowflake-like algorithm)
- [ ] Transactional consistency across storage layers
- [ ] Advanced graph traversal queries
- [ ] Real-time subscriptions and webhooks
- [ ] Additional content parsers (Markdown, HTML, PDF)
- [ ] Cross-node replication for distributed deployments
- [ ] SPARQL-like query language
- [ ] Performance monitoring dashboard

---

**SpaceDb** - Building the foundation for intelligent AI memory systems.
