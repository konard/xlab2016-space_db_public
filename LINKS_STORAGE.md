# Links Storage Integration

## Overview

SpaceDb now includes an alternative graph storage mechanism using [Platform.Data.Doublets](https://github.com/linksplatform/Data.Doublets), implementing the [Links Notation](https://github.com/link-foundation/links-notation) protocol. This provides a complementary approach to the existing RocksDB+Qdrant storage for managing resource-block-fragment hierarchies.

## Architecture

### What are Links?

Links are fundamental data units that represent directed relationships. Each link consists of:
- **Index**: Unique identifier (ulong)
- **Source**: Reference to another link (ulong)
- **Target**: Reference to another link (ulong)

This simple structure enables building complex hierarchical and graph data structures.

### Integration with SpaceDb

The Links storage complements the existing SpaceDb storage by providing:

1. **Alternative Graph Storage**: Instead of storing segments in RocksDB, you can use Links for graph relationships
2. **Resource Hierarchy Mapping**: Store resource-block-fragment structures as link hierarchies
3. **Native Graph Traversal**: Efficient graph queries using Platform.Data.Doublets

### Storage Structure for Resource-Block-Fragment

```
Resource (ResourceType, ResourceId)
  ├─> Block 1 (BlockType, BlockId)
  │    ├─> Fragment 1 (FragmentType, FragmentId)
  │    └─> Fragment 2 (FragmentType, FragmentId)
  └─> Block 2 (BlockType, BlockId)
       ├─> Fragment 3 (FragmentType, FragmentId)
       └─> Fragment 4 (FragmentType, FragmentId)
```

Well-known link types:
- `ResourceType` (ID: 1)
- `BlockType` (ID: 2)
- `FragmentType` (ID: 3)
- `ContainsRelation` (ID: 4)

## Configuration

The Links database is stored in a file-mapped memory location:

**Environment Variable:**
```bash
export LINKS_DB_PATH=/path/to/linksdb
```

**Default Location:**
```
./linksdb/db.links
```

## API Usage

### Basic Link Operations

#### Create a Link

```bash
POST /api/v1/links
Content-Type: application/json
Authorization: Bearer <token>

{
  "source": 100,
  "target": 200
}
```

**Response:**
```json
{
  "data": {
    "linkId": 5,
    "source": 100,
    "target": 200
  },
  "message": "Link 5 created successfully"
}
```

#### Get a Link

```bash
GET /api/v1/links/5
Authorization: Bearer <token>
```

**Response:**
```json
{
  "data": {
    "id": 5,
    "source": 100,
    "target": 200
  },
  "message": "Link retrieved successfully"
}
```

#### Update a Link

```bash
PUT /api/v1/links/5
Content-Type: application/json
Authorization: Bearer <token>

{
  "newSource": 300,
  "newTarget": 400
}
```

#### Delete a Link

```bash
DELETE /api/v1/links/5
Authorization: Bearer <token>
```

### Search Operations

#### Search by Source

```bash
GET /api/v1/links/search/by-source/100
Authorization: Bearer <token>
```

**Response:**
```json
{
  "data": [5, 6, 7],
  "count": 3,
  "message": "Found 3 links with source 100"
}
```

#### Search by Target

```bash
GET /api/v1/links/search/by-target/200
Authorization: Bearer <token>
```

#### Search by Source AND Target

```bash
GET /api/v1/links/search?source=100&target=200
Authorization: Bearer <token>
```

#### Count All Links

```bash
GET /api/v1/links/count
Authorization: Bearer <token>
```

**Response:**
```json
{
  "data": 1523,
  "message": "Total links: 1523"
}
```

### Resource Hierarchy Operations

#### Store Resource-Block-Fragment Hierarchy

```bash
POST /api/v1/links/resource-hierarchy
Content-Type: application/json
Authorization: Bearer <token>

{
  "resourceId": 1000,
  "blockIds": [2000, 2001],
  "fragmentIdsByBlock": {
    "2000": [3000, 3001, 3002],
    "2001": [3003, 3004]
  }
}
```

**Response:**
```json
{
  "data": {
    "resourceLinkId": 15,
    "resourceId": 1000
  },
  "message": "Resource hierarchy stored successfully with link ID 15"
}
```

#### Retrieve Resource Hierarchy

```bash
GET /api/v1/links/resource-hierarchy/15
Authorization: Bearer <token>
```

**Response:**
```json
{
  "data": {
    "resourceId": 1000,
    "blocks": [
      {
        "blockId": 2000,
        "fragmentIds": [3000, 3001, 3002]
      },
      {
        "blockId": 2001,
        "fragmentIds": [3003, 3004]
      }
    ]
  },
  "message": "Retrieved resource hierarchy for link ID 15"
}
```

## Use Cases

### 1. Alternative to RocksDB Segments

Instead of storing segments in RocksDB, use Links for graph relationships:

**Traditional Approach:**
```
RocksDB: segment:out:{fromId}:{toId}
RocksDB: segment:in:{toId}:{fromId}
```

**Links Approach:**
```
Link: (fromId, toId)
```

**Benefits:**
- Native graph traversal
- Bidirectional search without storing both directions
- Memory-mapped file storage (persistent + fast)

### 2. Resource-Block-Fragment Storage

Store parsed content hierarchies using Links:

```csharp
var resourceLinkId = await linksService.StoreResourceHierarchyAsync(
    resourceId: 1000,
    blockIds: new[] { 2000, 2001 },
    fragmentIdsByBlock: new Dictionary<long, IEnumerable<long>>
    {
        { 2000, new[] { 3000L, 3001L, 3002L } },
        { 2001, new[] { 3003L, 3004L } }
    });

// Later retrieve the hierarchy
var hierarchy = await linksService.GetResourceHierarchyAsync(resourceLinkId);
```

### 3. Graph Traversal

Navigate the graph efficiently:

```csharp
// Find all blocks for a resource
var resourceId = 1000;
var resourceLinks = await linksService.SearchBySourceAsync(resourceId);

// Find all fragments for a block
var blockId = 2000;
var fragmentLinks = await linksService.SearchBySourceAsync(blockId);
```

## Comparison: RocksDB+Qdrant vs Links Storage

| Feature | RocksDB+Qdrant | Links Storage |
|---------|----------------|---------------|
| **Graph Relationships** | Manual bidirectional storage | Native graph links |
| **Search** | Vector similarity (semantic) | Structure-based (graph) |
| **Storage** | Key-value + vectors | Memory-mapped links |
| **Query Type** | Semantic search | Graph traversal |
| **Performance** | Excellent for similarity | Excellent for relationships |
| **Use Case** | AI/ML embeddings | Hierarchical structures |

**Recommendation:** Use both!
- **RocksDB+Qdrant**: For semantic search over content (embeddings)
- **Links Storage**: For hierarchical relationships and graph traversal

## Performance Considerations

### Storage Efficiency

- **File-mapped memory**: Fast access, persistent storage
- **Minimal overhead**: Each link is just 3 ulongs (index, source, target)
- **Compact**: No JSON serialization overhead like RocksDB

### Query Performance

- **Search by source/target**: O(n) scan with early termination
- **Get by ID**: O(1) direct access
- **Graph traversal**: Efficient due to memory-mapped structure

### Scalability

- Platform.Data.Doublets handles millions of links efficiently
- Memory-mapped files allow working with data larger than RAM
- Generic type support (`ulong`) provides large address space

## Testing

Comprehensive tests are available in `SpaceDb.Tests/Services/LinksServiceTests.cs`:

```bash
dotnet test --filter "FullyQualifiedName~LinksServiceTests"
```

Test coverage includes:
- Basic CRUD operations
- Search functionality
- Resource hierarchy storage and retrieval
- Edge cases (empty hierarchies, non-existent links)

## Implementation Details

### LinksService

**Location:** `src/SpaceDb/Services/LinksService.cs`

**Key Methods:**
- `CreateLinkAsync(source, target)`: Create new link
- `GetLinkAsync(linkId)`: Retrieve link data
- `UpdateLinkAsync(linkId, newSource, newTarget)`: Modify link
- `DeleteLinkAsync(linkId)`: Remove link
- `SearchBySourceAsync(source)`: Find links with matching source
- `SearchByTargetAsync(target)`: Find links with matching target
- `StoreResourceHierarchyAsync(...)`: Store hierarchy as links
- `GetResourceHierarchyAsync(linkId)`: Retrieve hierarchy structure

### Dependency Injection

Registered in `Helpers/StartupHelper.cs`:

```csharp
services.AddSingleton<ILinksService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<LinksService>>();
    var dbPath = Environment.GetEnvironmentVariable("LINKS_DB_PATH") ??
                 Path.Combine(Directory.GetCurrentDirectory(), "linksdb");
    return new LinksService(dbPath, logger);
});
```

### Database Structure

Platform.Data.Doublets uses `UnitedMemoryLinks<ulong>` with:
- Generic type: `ulong` (unsigned 64-bit integer)
- Storage: File-mapped resizable direct memory
- File: `{LINKS_DB_PATH}/db.links`

## Troubleshooting

### "Failed to initialize LinksService"

**Cause:** Insufficient permissions or invalid path

**Solution:**
```bash
# Ensure directory exists and is writable
mkdir -p ./linksdb
chmod 755 ./linksdb
```

### "Link not found"

**Cause:** Link ID doesn't exist or was deleted

**Solution:**
- Verify link ID is correct
- Check if link was deleted
- Use `CountLinksAsync()` to see total links

### Performance Degradation

**Cause:** Too many links in memory

**Solution:**
- Platform.Data.Doublets scales well to millions of links
- If experiencing issues, check disk space for memory-mapped file
- Consider compacting the database periodically

## Future Enhancements

Potential improvements:
1. **Batch Operations**: Create/update/delete multiple links in one call
2. **Graph Algorithms**: Shortest path, connected components, cycle detection
3. **Export/Import**: Serialize hierarchies to/from JSON
4. **Indexing**: Add custom indexes for faster queries
5. **Transactions**: Support for atomic multi-link operations

## References

- [Platform.Data.Doublets GitHub](https://github.com/linksplatform/Data.Doublets)
- [Links Notation Spec](https://github.com/link-foundation/links-notation)
- [Link-CLI Tool](https://github.com/link-foundation/link-cli)
- [LinksPatform Documentation](https://linksplatform.github.io/)

## License

This integration uses Platform.Data.Doublets which is licensed under LGPL-3.0.
