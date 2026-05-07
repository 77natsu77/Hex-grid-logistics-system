using System;
using System.Collections;
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
            // ADDED FUEL CONSTRAINT
            // I WILL WORK TOWARDS ADDING MORE CONSTRAINTS AND OBJECTIVE FUNCTIONS AND STUFF IN THE FUTURE AS THIS PROJECT WAS MEANT TO BE GRAPH THEORY PRACTICE I FORGOT ABOUT
            HexNode[] map = new HexNode[1000000];
            int radius = 300;
            HexGrid myGrid = new HexGrid(radius);
            myGrid.GenerateMap();

            Console.WriteLine($"Map generated with radius {radius}.");

            // Test a coordinate
            HexCoords center = new HexCoords(0, 0, 0);
            HexNode centerNode = myGrid.GetNodeByCoords(center);

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
            HexCoords destCoords = new HexCoords(50, 50, -100);
            HexNode DestinationNode = myGrid.GetNodeByCoords(destCoords);

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
                // Pass the agent's fuel and set ignoreFuelConstraint to true to get diagnostic data
                PathResult result = myPathfinder.FindPath(myGrid, centerNode, DestinationNode, 100, true);
                switch (result.Status)
                {
                    case PathStatus.Success:
                        Console.WriteLine($"Path found! It will cost {result.TotalCost} fuel.");
                        foreach (int index in result.Path)
                        {
                            HexNode node = myGrid.GetNodeByIndex(index);
                            Console.WriteLine($"Q:{node.Coords.Q} R: {node.Coords.R} S: {node.Coords.S}");
                        }
                        break;
                    case PathStatus.TooExpensive:
                        int deficit = result.TotalCost - 100; // Assuming 100 was the fuel passed in
                        Console.WriteLine($"Path found, but it is too expensive! It costs {result.TotalCost} fuel.");
                        Console.WriteLine($"You need {deficit} more fuel to complete this trip.");
                        break;
                    case PathStatus.Unreachable:
                        Console.WriteLine("No path possible. The destination is unreachable.");
                        break;
                    default:
                        Console.WriteLine("An Error occured while generating the path.");
                        break;
                }
            }


            Console.ReadLine();
        }

        public enum PathStatus { Success, TooExpensive, Unreachable }

        public struct PathResult
        {
            public List<int> Path;
            public int TotalCost;
            public PathStatus Status;

            public PathResult(List<int> path, int totalCost, PathStatus status)
            {
                this.Path = path;
                this.TotalCost = totalCost;
                this.Status = status;
            }

            // A helper to quickly check if we can actually move
            public bool IsViable => Status == PathStatus.Success;
        }
        class HexGrid
        {
            private HexNode[] _nodes;
            private int _radius;
            private int _width;

            public HexGrid(int radius)
            {
                _radius = radius;
                _width = 2 * _radius + 1;
                _nodes = new HexNode[_width * _width];
            }

            // Helper to get the index directly for the Pathfinder to use
            public int GetIndex(HexCoords coords)
            {
                return (coords.R + _radius) * _width + (coords.Q + _radius);
            }

            public HexNode GetNodeByIndex(int index) => _nodes[index]; // should add exception handling here...

            public int GetTotalSize() =>  _width * _width;

            public HexNode GetNodeByCoords(HexCoords coords)
            {
                //  Math-based Bounds Check
                if (Math.Abs(coords.Q) > _radius || Math.Abs(coords.R) > _radius || Math.Abs(coords.S) > _radius)
                {
                    return new HexNode(); // Out of bounds, return empty struct
                }
                // Get index from flattened 2D array
                int q_offset = coords.Q + _radius;
                int r_offset = coords.R + _radius;
                int i = r_offset * _width + q_offset;

                return _nodes[i];
            }

            public List<HexNode> GetNeighbors(HexNode center)
            {
                List<HexNode> neighbours = new List<HexNode>();
                // We calculate the 6 possible neighbor coordinates and look them up.
                foreach (HexCoords currentCoords in HexCoords.Directions)
                {

                    HexCoords newCoords = center.Coords + currentCoords;
                    HexNode newNode = GetNodeByCoords(newCoords);
                    if (newNode.hasValue) neighbours.Add(newNode);
                }

                return neighbours;
            }
            public List<int> GetNeighborIndices(int centerIndex)
            {
                HexNode center = GetNodeByIndex(centerIndex);
                List<int> neighbourIndices = new List<int>();
                // We calculate the 6 possible neighbor coordinates and look them up.
                foreach (HexCoords currentCoords in HexCoords.Directions)
                {

                    HexCoords newCoords = center.Coords + currentCoords;
                    HexNode newNode = GetNodeByCoords(newCoords);
                    if (newNode.hasValue) neighbourIndices.Add(GetIndex(newNode.Coords));
                }

                return neighbourIndices;
            }

            public void GenerateMap()
            {
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
                        int i = r_offset * _width + q_offset; // Essentially reducing a 2d list to a 1d one, y * width + x, but we add an offset because we loop from -radius->radius, and a negative number would give an out of inder error
                        _nodes[i] = newNode;
                    }
                }
            }

            public int GetHeuristic(int i, int j) => GetNodeByIndex(i).Coords.GetDistance(GetNodeByIndex(j).Coords);
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
                hasValue = true;
            }
        }

        class Pathfinder
        {
            private int[] _cameFrom;
            private int[] _costSoFar;
            private PathResult _result;
            public PathResult FindPath(HexGrid grid, HexNode start, HexNode end, int FuelAvailable, bool ignoreFuelConstraint = false)
            {
                _result = new PathResult(new List<int>(), 0, PathStatus.Unreachable);
                int PathCost = 0;
                int mapSize = grid.GetTotalSize(); // width * width
                int endIndex = grid.GetIndex(end.Coords);
                int startIndex = grid.GetIndex(start.Coords);
                // parallel arrays
                _cameFrom = new int[mapSize];
                _costSoFar = new int[mapSize];

                // Initialize with sentinels, Array.Fill not available on current .Net version...

                for (int i = 0; i < _cameFrom.Length; i++) { _cameFrom[i] = -1; }
                for (int i = 0; i < _costSoFar.Length; i++) { _costSoFar[i] = int.MaxValue; }

                // 2. PriorityQueue <NodeIndex, Priority>
                // Priority is f(n) = g(n) + h(n)
                var openSet = new SimplePriorityQueue();

                _costSoFar[startIndex] = 0;
                openSet.Enqueue(startIndex, 0);

                while (openSet.Count > 0)
                {
                    int current = openSet.Dequeue();

                    if (current == endIndex)
                    {
                        // RECONSTRUCT PATH
                        List<int> path = new List<int>();
                        int temp = endIndex;
                        while (temp != -1)
                        {
                            path.Add(temp);
                            temp = _cameFrom[temp];
                        }
                        path.Reverse();
                        PathCost = LogisticsEngine.CalculatePathCost(path, grid);
                        _result.Path = path;
                        _result.Status = LogisticsEngine.CanMakeTrip(FuelAvailable, PathCost) ? PathStatus.Success : PathStatus.TooExpensive;
                        _result.TotalCost = LogisticsEngine.CalculatePathCost(path, grid);
                        return _result;
                    }

                    foreach (int neighbor in grid.GetNeighborIndices(current))
                    {
                        // Skip invalid/unpassable neighbors/paths that exceed cost
                        var node = grid.GetNodeByIndex(neighbor);
                        int newCost = _costSoFar[current] + node.MovementCost;
                        if (!node.hasValue || !node.IsPassable || (!ignoreFuelConstraint && (newCost > FuelAvailable))) continue;
                                           


                        if (newCost < _costSoFar[neighbor])
                        {
                            _costSoFar[neighbor] = newCost;
                            _cameFrom[neighbor] = current;

                            // Priority = Cost + Heuristic
                            int priority = newCost + grid.GetHeuristic(neighbor, endIndex);
                            openSet.Enqueue(neighbor, priority);
                        }
                    }
                }
                return _result; // No path found
            }
        }

        class Agent
        {
            public HexNode CurrentLocation;
            public int MovementPoints;
            private int _maxFuel = 200;
            private int _currentFuel = 100;

            public void ConsumeFuel(int ConsumedFuel) => _currentFuel = Math.Max(0, _currentFuel - ConsumedFuel);
            
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
                return !(a == b);
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

        static class LogisticsEngine
        {
            static public int CalculatePathCost(List<int> path, HexGrid grid)
            {
                int pathCost = 0;
                for (int i = 1; i < path.Count; i++)
                {
                    pathCost += grid.GetNodeByIndex(path[i]).MovementCost; // Get the index of the Node on tha path and its movement cost to the total
                }
                return pathCost;
            }

            static public bool CanMakeTrip(int fuelAvailable, int pathCost) => (pathCost <= fuelAvailable);

        }

        public class SimplePriorityQueue // Had to use this since the normal priority queue wasn't available
        {
            // Flattening to a 1D array once again, this time a tree (as this is implemented as a binary min-heap)
            //Its Left Child is always at index 2i + 1
            // Its Right Child is always at index 2i + 2
            //Its Parent is always at index $(i - 1) / 2 (using integer division)

            // Stores tuples of (NodeIndex, PriorityScore)
            private List<(int Item, int Priority)> _elements = new List<(int, int)>();

            public int Count => _elements.Count;

            public void Enqueue(int item, int priority)
            {
                _elements.Add((item, priority));

                // "Bubble Up" the new item to its correct position
                int childIndex = _elements.Count - 1;
                while (childIndex > 0)
                {
                    int parentIndex = (childIndex - 1) / 2;

                    // If the child is worse or equal to the parent, stop
                    if (_elements[childIndex].Priority >= _elements[parentIndex].Priority)
                        break;

                    // Swap child and parent
                    var tmp = _elements[childIndex];
                    _elements[childIndex] = _elements[parentIndex];
                    _elements[parentIndex] = tmp;

                    childIndex = parentIndex;
                }
            }

            public int Dequeue()
            {
                int firstItem = _elements[0].Item;
                int lastIndex = _elements.Count - 1;

                // Move the last item to the top and remove the duplicate at the end
                _elements[0] = _elements[lastIndex];
                _elements.RemoveAt(lastIndex);
                lastIndex--;

                // "Bubble Down" the new top item to its correct position
                int parentIndex = 0;
                while (true)
                {
                    int leftChildIndex = parentIndex * 2 + 1;
                    if (leftChildIndex > lastIndex) break; // No children left

                    int rightChildIndex = leftChildIndex + 1;
                    int minIndex = leftChildIndex;

                    // Find the smaller of the two children
                    if (rightChildIndex <= lastIndex && _elements[rightChildIndex].Priority < _elements[leftChildIndex].Priority)
                    {
                        minIndex = rightChildIndex;
                    }

                    // If the parent is already better than both children, stop
                    if (_elements[parentIndex].Priority <= _elements[minIndex].Priority)
                        break;

                    // Swap parent and the smallest child
                    var tmp = _elements[parentIndex];
                    _elements[parentIndex] = _elements[minIndex];
                    _elements[minIndex] = tmp;

                    parentIndex = minIndex;
                }

                return firstItem;
            }
        }
    }
}


