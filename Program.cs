using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Hex_Grid_logistics_system
{
    internal class Program
    {
        static void Main(string[] args)
        {
	// trying to collapse data structures to obtain as much O(1) as possible and have millions of nodes at a time
            HexNode[] map = new HexNode[1000000];
            int radius = 5;
            HexGrid myGrid = new HexGrid();
            myGrid.GenerateMap();

            Console.WriteLine($"Map generated with radius {radius}.");

            // Test a coordinate
            HexCoords center = new HexCoords(0, 0, 0);
            HexNode centerNode = myGrid.GetNode(center);

            if (centerNode.hasValue)
            {
                Console.WriteLine("Center node found. Finding neighbors...");
                var neighbors = myGrid.GetNeighbors(centerNode);
                Console.WriteLine($"Found {neighbors.Count} neighbors.");

                foreach (var n in neighbors)
                {
                    Console.WriteLine($"Neighbor at: Q:{n.Coords.Q} R:{n.Coords.R} S:{n.Coords.S}");
                }
            }

            Agent myAgent = new Agent(centerNode);
            Pathfinder myPathfinder = new Pathfinder();
            HexCoords destCoords = new HexCoords(5, -3, -2);
            HexNode DestinationNode = myGrid.GetNode(destCoords);

            if (!DestinationNode.hasValue)
            {
                Console.WriteLine("Destination is outside the map radius!");
            }
            else if (!DestinationNode.IsPassable)
            {
                Console.WriteLine("Destination is a mountain! Choose a different spot.");
            }
            else
            {
                List<HexNode> path = myPathfinder.FindPath(myGrid, centerNode, DestinationNode);

                if (path != null)
                {
                    Console.WriteLine("path: ");
                    foreach (HexNode node in path)
                    {
                        Console.WriteLine($"Q:{node.Coords.Q} R: {node.Coords.R} S: {node.Coords.S}");
                    }
                }
                else
                {
                    Console.WriteLine("No path possible");
                }
            }


            Console.ReadLine();
        }

        class HexGrid
        {
            private HexNode[] _nodes;
            private int _radius;

            public HexGrid(int radius) => _radius = radius;
            public HexNode GetNode(HexCoords coords)
            {
                //  Math-based Bounds Check
                if (Math.Abs(coords.Q) > _radius || Math.Abs(coords.R) > _radius || Math.Abs(coords.S) > _radius)
                {
                    return new HexNode(); // Out of bounds, return empty struct
                }
                // Get index from flattened 2D array
                int width = _radius * 2 + 1;
                int q_offset = coords.Q + _radius;
                int r_offset = coords.R + _radius;
                int i = r_offset * width + q_offset;

                return _nodes[i];
            }
            public List<HexNode> GetNeighbors(HexNode center)
            {
                List<HexNode> neighbours = new List<HexNode>();
                // We calculate the 6 possible neighbor coordinates and look them up.
                foreach (HexCoords currentCoords in HexCoords.Directions)
                {

                    HexCoords newCoords = center.Coords + currentCoords;
                    HexNode newNode = GetNode(newCoords);
                    if (newNode.hasValue) neighbours.Add(newNode);
                }

                return neighbours;
            }

            public void GenerateMap()
            {
                int width = 2 * _radius + 1;
                _nodes = new HexNode[width * width];
                Random rand = new Random();
                for (int q = -_radius; q <= _radius; q++)
                {
                    for (int r = Math.Max(-_radius, -q - _radius); r <= Math.Min(_radius, -q + _radius); r++)
                    {
                        int s = -q - r;
                        int SwampChance = rand.Next(0, 100);
                        int MountainChance = rand.Next(0, 100);
                        TerrainType terrainType = new TerrainType("Plains", 1, true);
                        if (SwampChance < 20)
                        {
                            terrainType = new TerrainType("Swamp", 5, true);
                        }
                        else if (MountainChance < 10)
                        {
                            terrainType = new TerrainType("Mountain", 1, false);
                        }

                        HexCoords coords = new HexCoords(q, r, s);
                        HexNode newNode = new HexNode(coords, terrainType);
                        int q_offset = q + _radius;
                        int r_offset = r + _radius;
                        int i = r_offset * width + q_offset; // Essentially reducing a 2d list to a 1d one, y * width + x, but we add an offset because we loop from -radius->radius, and a negative number would give an out of inder error
                        _nodes[i] = newNode;
                    }
                }
            }
        }

         public  struct HexNode
        {
             public HexCoords Coords { get; }
            public int MovementCost { get; } // 1 for plains, 5 for swamp
            public bool IsPassable { get; }  // Some tiles (Mountains/Deep Water) might be 0
            public TerrainType Type { get; } // Each terrain has different special rules
            public bool hasValue { get; }
            public HexNode(HexCoords coords, TerrainType type)
            {
                Coords = coords;
                Type = type;
                MovementCost = Type.movementCost;
                IsPassable = Type.passable;
                hasValue = false;
            }
        }

        class Pathfinder
        {

            private float Heuristic(HexNode a, HexNode b)
            {
                // A* performs best when the heuristic is mathematically sound!
                return a.Coords.GetDistance(b.Coords);
            }

            public List<HexNode> FindPath(HexGrid grid, HexNode start, HexNode end)
            {
                List<HexNode> openSet = new List<HexNode> { start };
                Dictionary<HexNode, HexNode> cameFrom = new Dictionary<HexNode, HexNode>();
                Dictionary<HexNode, int> costSoFar = new Dictionary<HexNode, int>();

                cameFrom[start] = new HexNode();
                costSoFar[start] = 0;

                while (openSet.Count > 0)
                {
                    // Sort and pick the best node
                    HexNode current = openSet.OrderBy(n => costSoFar[n] + Heuristic(n, end)).First();

                    //  Remove it so we don't process it again
                    openSet.Remove(current);

                    if (current.Coords == end.Coords)
                    {
                        // RECONSTRUCT PATH
                        List<HexNode> path = new List<HexNode>();
                        HexNode temp = end;
                        while (temp.hasValue)
                        {
                            path.Add(temp);
                            temp = cameFrom[temp];
                        }
                        path.Reverse(); // Turn End->Start into Start->End
                        return path;
                    }

                    foreach (HexNode neighbor in grid.GetNeighbors(current))
                    {
                        if (!neighbor.IsPassable) continue; // Skip walls/mountains

                        int newCost = costSoFar[current] + neighbor.MovementCost;

                        if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor]) //it hasn't been recorded yet or a better path, one with a lower cost
                        {
                            costSoFar[neighbor] = newCost;
                            cameFrom[neighbor] = current;

                            if (!openSet.Contains(neighbor))
                                openSet.Add(neighbor);
                        }
                    }
                }
                return null; // No path possible
            }
        }

        class Agent
        {
            public HexNode CurrentLocation;
            public int MovementPoints;

            public void Requestpath(HexNode destination)
            {

            }

            public void MoveAlongPath(List<HexNode> path)
            {
                int remainingPoints = MovementPoints;

                // Logic: 
                foreach (HexNode nextNode in path.Skip(1))
                {
                    if (remainingPoints >= nextNode.MovementCost)
                    {
                        CurrentLocation = nextNode;
                        remainingPoints -= nextNode.MovementCost;
                    }
                    if (remainingPoints == 0) break;
                }
            }

            public Agent(HexNode location)
            {
                CurrentLocation = location;
            }
        }

         public struct HexCoords
        {
            public int Q { get; }
            public int R { get; }
            public int S { get; }

            public HexCoords(int q, int r, int s)
            {
                if (q + r + s != 0)
                    throw new ArgumentException("Sum of Hex coordinates (q+r+s) must be 0.");

                Q = q; R = r; S = s;
            }

            public int GetDistance(HexCoords target)
            {
                return (Math.Abs(Q - target.Q) + Math.Abs(R - target.R) + Math.Abs(S - target.S)) / 2;
            }

            // This makes getting neighbors incredibly easy later
            public static HexCoords[] Directions = {
                new HexCoords(1, -1, 0), new HexCoords(1, 0, -1), new HexCoords(0, 1, -1),
                new HexCoords(-1, 1, 0), new HexCoords(-1, 0, 1), new HexCoords(0, -1, 1)
                };

            public static HexCoords operator +(HexCoords a, HexCoords b)
            {
                return new HexCoords(a.Q + b.Q, a.R + b.R, a.S + b.S);
            }
            public static HexCoords operator -(HexCoords a, HexCoords b)
            {
                return new HexCoords(a.Q - b.Q, a.R - b.R, a.S - b.S);
            }
            public static HexCoords operator *(HexCoords a, HexCoords b)
            {
                return new HexCoords(a.Q * b.Q, a.R * b.R, a.S * b.S);
            }
            public static HexCoords operator /(HexCoords a, HexCoords b)
            {
                return new HexCoords(a.Q / b.Q, a.R / b.R, a.S / b.S);
            }
            public static bool operator ==(HexCoords a, HexCoords b)
            {
                return a.Q == b.Q && a.R == b.R && a.S == b.S;
            }
            public static bool operator !=(HexCoords a, HexCoords b)
            {
                return a.Q != b.Q && a.R != b.R && a.S != b.S;
            }

        }

        public struct TerrainType
        {
            public string type { get; }
            public int movementCost { get; }
            public bool passable { get; }

            public TerrainType(string type, int movementCost, bool passable)
            {
                this.type = type;
                this.movementCost = movementCost;
                this.passable = passable;
            }
        }

        class LogisticsEngine
        {
            public int CalculateTotalFuel(List<HexNode> path)
            {
                if (path == null || path.Count <= 1) return 0;

                // We don't pay for the tile we are already standing on.
                // Skip(1) starts the sum from the first move.
                return path.Skip(1).Sum(node => node.MovementCost);
            }
        }
    }
}


