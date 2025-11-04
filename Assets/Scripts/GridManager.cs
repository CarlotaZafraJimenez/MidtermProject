using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

public class GridManager : MonoBehaviour
{
    [Header("Map Settings")]
    public int width = 10;
    public int height = 10;
    public GameObject tilePrefab;
    public string valueMap;
    public const int MAX_MOVE_COST = 5;

    [Header("Materials")]
    public Material lightMaterial;
    public Material darkMaterial;
    public Gradient terrainColors;

    public Tile[,] map;

    void Awake()
    {
        map = new Tile[width, height];
        GenerateGrid();
    }

    // Instantiates a grid of tiles with colors based on movement cost
    public void GenerateGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 tilePosition = new Vector3(x - width / 2f, 0, y - height / 2f);
                GameObject tile = Instantiate(tilePrefab, tilePosition, Quaternion.identity);
                tile.name = $"Tile {x},{y}";
                tile.transform.SetParent(transform);

                Renderer renderer = tile.GetComponentInChildren<Renderer>();
                renderer.material = new Material((x + y) % 2 == 0 ? lightMaterial : darkMaterial);

                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.gridPosition = new Vector2Int(x, y);

                // ✅ Fix 1: Correct index order and protect against short valueMap
                int index = y * width + x;
                if (!string.IsNullOrEmpty(valueMap) && index < valueMap.Length)
                    tileScript.moveCost = Mathf.Clamp(valueMap[index] - '0', 1, MAX_MOVE_COST);
                else
                    tileScript.moveCost = 1; // default if missing

                float normalizedCost = (float)tileScript.moveCost / MAX_MOVE_COST;
                tileScript.originalColor = tileScript.moveCost > MAX_MOVE_COST
                    ? Color.red
                    : terrainColors.Evaluate(normalizedCost);

                map[x, y] = tileScript;
            }
        }
    }

    // Retrieves the neighbors of the given tile position
    private List<Tile> GetTileNeighbors(Vector2Int tilePosition, bool includeDiagonals)
    {
        List<Tile> neighbors = new List<Tile>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int posX = tilePosition.x + x;
                int posY = tilePosition.y + y;

                if (posX < 0 || posY < 0 || posX >= width || posY >= height) continue;

                Tile current = map[posX, posY];
                if (current.gridPosition == tilePosition) continue;

                if (!includeDiagonals && IsDiagonal(map[tilePosition.x, tilePosition.y], current)) continue;

                neighbors.Add(current);
            }
        }

        return neighbors;
    }

    private bool IsDiagonal(Tile a, Tile b)
    {
        int dx = Mathf.Abs(a.gridPosition.x - b.gridPosition.x);
        int dy = Mathf.Abs(a.gridPosition.y - b.gridPosition.y);
        return dx == 1 && dy == 1;
    }

    public void ResetGridHighlights()
    {
        foreach (Tile tile in map)
        {
            if (Tile.selectedTile != tile)
            {
                tile.inMoveRange = false;
                tile.inAttackRange = false;
                tile.ChangeColor(tile.originalColor);
            }
        }
    }

    public List<Tile> GetHighlightRange(Vector2Int start, int moveRange, int attackRange)
    {
        ResetGridHighlights();

        List<Tile> moveTiles = new List<Tile>();
        Dictionary<Tile, int> costSoFar = new Dictionary<Tile, int>();
        Queue<Tile> edge = new Queue<Tile>();

        Tile startTile = map[start.x, start.y];
        edge.Enqueue(startTile);
        costSoFar[startTile] = 0;

        while (edge.Count > 0)
        {
            Tile current = edge.Dequeue();
            int currentCost = costSoFar[current];

            foreach (Tile neighbor in GetTileNeighbors(current.gridPosition, true))
            {
                int stepCost = IsDiagonal(current, neighbor) ? 1 + neighbor.moveCost : neighbor.moveCost;
                int newCost = currentCost + stepCost;

                if (newCost <= moveRange &&
                    (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor]) &&
                    !neighbor.isOccupied)
                {
                    costSoFar[neighbor] = newCost;
                    edge.Enqueue(neighbor);

                    if (!moveTiles.Contains(neighbor) && neighbor != startTile)
                    {
                        moveTiles.Add(neighbor);
                        neighbor.inMoveRange = true;
                    }
                }
            }
        }

        HashSet<Tile> attackTiles = new HashSet<Tile>();
        foreach (Tile origin in moveTiles.Concat(new List<Tile> { startTile }))
        {
            Queue<(Tile tile, int distance)> attackQueue = new Queue<(Tile, int)>();
            attackQueue.Enqueue((origin, 0));
            HashSet<Tile> visited = new HashSet<Tile> { origin };

            while (attackQueue.Count > 0)
            {
                var (tile, distance) = attackQueue.Dequeue();

                foreach (Tile neighbor in GetTileNeighbors(tile.gridPosition, false))
                {
                    if (!visited.Contains(neighbor) && distance + 1 <= attackRange)
                    {
                        visited.Add(neighbor);
                        attackQueue.Enqueue((neighbor, distance + 1));
                        attackTiles.Add(neighbor);
                        neighbor.inAttackRange = true;
                    }
                }
            }
        }

        return moveTiles.Concat(attackTiles).Distinct().ToList();
    }

    public void HighlightRange(Tile start, int moveRange, int attackRange)
    {
        List<Tile> reachableTiles = GetHighlightRange(start.gridPosition, moveRange, attackRange);

        foreach (Tile tile in reachableTiles)
        {
            if (tile.inMoveRange) tile.ChangeColor(Color.cyan);
            else if (tile.inAttackRange) tile.ChangeColor(Color.red);
        }
    }

    public Tile GetTile(Vector2Int position)
    {
        position.x = Mathf.Clamp(position.x, 0, width - 1);
        position.y = Mathf.Clamp(position.y, 0, height - 1);
        return map[position.x, position.y];
    }

    public Tile GetTile(Vector3 position)
    {
        
        float offsetX = width / 2f;
        float offsetY = height / 2f;

        int x = Mathf.FloorToInt(position.x + offsetX);
        int y = Mathf.FloorToInt(position.z + offsetY);

        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);

        return map[x, y];
    }

    public int GetHeuristic(Tile start, Tile end)
    {
        return Mathf.Abs(start.gridPosition.x - end.gridPosition.x) + Mathf.Abs(start.gridPosition.y - end.gridPosition.y);
    }

    private List<Tile> RetracePath(Tile start, Tile end)
    {
        List<Tile> path = new List<Tile>();
        Tile current = end;

        while (current != start)
        {
            path.Add(current);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    public List<Tile> GetPath(Tile start, Tile end)
    {
        List<Tile> open = new List<Tile>();
        HashSet<Tile> closed = new HashSet<Tile>();

        open.Add(start);
        start.gCost = 0;
        start.hCost = GetHeuristic(start, end);
        start.parent = null;

        while (open.Count > 0)
        {
            Tile current = open.OrderBy(t => t.fCost).ThenBy(t => t.hCost).First();

            if (current == end)
                return RetracePath(start, end);

            open.Remove(current);
            closed.Add(current);

            foreach (Tile neighbor in GetTileNeighbors(current.gridPosition, true))
            {
                if (closed.Contains(neighbor) || neighbor.isOccupied) continue;

                int tempG = current.gCost + neighbor.moveCost;

                if (!open.Contains(neighbor) || tempG < neighbor.gCost)
                {
                    neighbor.gCost = tempG;
                    neighbor.hCost = GetHeuristic(neighbor, end);
                    neighbor.parent = current;
                    if (!open.Contains(neighbor)) open.Add(neighbor);
                }
            }
        }

        return null;
    }

    public Tile GetClosestAttackTile(Unit target, Unit attacker)
    {
        List<Tile> attackTiles = new List<Tile>();
        int attackRange = attacker.attackRange;

        foreach (Tile tile in map)
        {
            if (tile.inMoveRange && !tile.isOccupied)
            {
                float distanceToTarget = GetHeuristic(GetTile(target.gridPosition), tile);
                if (distanceToTarget < attackRange)
                {
                    attackTiles.Add(tile);
                }
            }
        }

        Tile closestTile = null;
        float bestDistance = float.MaxValue;

        foreach (Tile tile in attackTiles)
        {
            float distanceToAttacker = GetHeuristic(GetTile(attacker.gridPosition), tile);
            if (distanceToAttacker < bestDistance)
            {
                closestTile = tile;
                bestDistance = distanceToAttacker;
            }
        }

        return closestTile;
    }
}
