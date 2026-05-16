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
            
            int radius = 300;
            HexGrid myGrid = new HexGrid(radius);
            myGrid.GenerateMap();

            HexCoords center = new HexCoords(0, 0, 0);
            HexNode centerNode = myGrid.GetNodeByCoords(center);
            Pathfinder myPathfinder = new Pathfinder(myGrid);

            Console.WriteLine($"Map generated with radius {radius}.");
            Agent myAgent = new Agent(centerNode,myGrid, myPathfinder);
            AgentJob newJob = new AgentJob { CargoAmount = 100, PickUpIndex = 15, DropOffIndex = 300 };
            myAgent.AssignJob(newJob);
            myAgent.PlanPath();

        }

        public enum PathStatus { Success, TooExpensive, Unreachable }
        public enum AgentState { Idle, EnRouteToPickup, EnRouteToDropoff }

        public struct AgentJob
        {
            public int PickUpIndex;
            public int DropOffIndex;
            public int CargoAmount;
        }
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
            private int _minCost = int.MaxValue;
            public int MinCost => _minCost;
            List<int> _refuelStationIndices = new List<int>();
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

            public List<int> GetRefuelStationIndices() => _refuelStationIndices;


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
                Random rand = new Random(); //TODO MOVE THIS RANDOM OUT OF HERE SO WE CAN ADDD SEEDS
                for (int q = -_radius; q <= _radius; q++)
                {
                    for (int r = Math.Max(-_radius, -q - _radius); r <= Math.Min(_radius, -q + _radius); r++)
                    {
                        int s = -q - r;
                        int q_offset = q + _radius;
                        int r_offset = r + _radius;
                        int i = r_offset * _width + q_offset; // Essentially reducing a 2d list to a 1d one, y * width + x, but we add an offset because we loop from -radius->radius, and a negative number would give an out of inder error

                        int SwampChance = rand.Next(0, 100);
                        int MountainChance = rand.Next(0, 100);
                        int OutpostChance = rand.Next(0, 100);
                        TerrainType terrainType = new TerrainType("Plains", 1, true);
                        if (SwampChance < 20) // CHECK IF A SWITCH WOULD BE FASTER
                        {
                            terrainType = new TerrainType("Swamp", 5, true);
                        }
                        else if (MountainChance < 5)
                        {
                            terrainType = new TerrainType("Mountain", 1, false);
                        }
                        else if (OutpostChance < 15)
                        {
                            terrainType = new TerrainType("Outpost", 1, false);
                            _refuelStationIndices.Add(i);
                        }

                         HexCoords coords = new HexCoords(q, r, s);
                        HexNode newNode = new HexNode(coords, terrainType);
                        _nodes[i] = newNode;

                        if (terrainType.movementCost < _minCost)// getting minimum movement cost for pathfinding refinement
                        {
                            _minCost = terrainType.movementCost;
                        }
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
           
            private HexGrid _grid;
            private PathResult _result;
            private int[] _cameFrom;
            private int[] _costSoFar;
            private int[] _nodeSearchID;
            private int _currentSearchID = -1;
            private List<int> _path;
            private SimplePriorityQueue openSet;

            public Pathfinder(HexGrid grid)
            {
                // Parallel arrays
                _cameFrom = new int[grid.GetTotalSize()];
                _costSoFar = new int[grid.GetTotalSize()];
                _nodeSearchID = new int[grid.GetTotalSize()];
            }
            public PathResult FindPath(HexGrid grid, HexNode start, HexNode end, int FuelAvailable, bool ignoreFuelConstraint = false)
            {
                _currentSearchID++;
                _result = new PathResult(new List<int>(), 0, PathStatus.Unreachable);
                int PathCost = 0;
                int mapSize = grid.GetTotalSize(); // width * width
                int endIndex = grid.GetIndex(end.Coords);
                int startIndex = grid.GetIndex(start.Coords);

                // 2. PriorityQueue <NodeIndex, Priority>
                // Priority is f(n) = g(n) + h(n)
                openSet = new SimplePriorityQueue();

                _costSoFar[startIndex] = 0;
                openSet.Enqueue(startIndex, 0);

                while (openSet.Count > 0)
                {
                    int currentIndex = openSet.Dequeue();

                    if (currentIndex == endIndex)
                    {
                        // RECONSTRUCT PATH
                        _path = new List<int>();
                        int temp = endIndex;
                        while (temp != -1)
                        {
                            _path.Add(temp);
                            temp = _cameFrom[temp];
                        }
                        _path.Reverse();
                        PathCost = LogisticsEngine.CalculatePathCost(_path, grid);
                        _result.Path = _path;
                        _result.Status = LogisticsEngine.CanMakeTrip(FuelAvailable, PathCost) ? PathStatus.Success : PathStatus.TooExpensive;
                        _result.TotalCost = LogisticsEngine.CalculatePathCost(_path, grid);
                        return _result;
                    }

                    foreach (int neighbor in grid.GetNeighborIndices(currentIndex))
                    {
                        // Skip invalid/unpassable neighbors/paths that exceed cost
                        var node = grid.GetNodeByIndex(neighbor);
                        int newCost = _costSoFar[currentIndex] + node.MovementCost;
                        if (!node.hasValue || !node.IsPassable || (!ignoreFuelConstraint && (newCost > FuelAvailable))) continue;
                        if (_nodeSearchID[currentIndex] != _currentSearchID)
                        {
                            _cameFrom[currentIndex] = -1;
                            _costSoFar[currentIndex] = int.MaxValue;
                        }


                        if (newCost < _costSoFar[neighbor])
                        {
                            _costSoFar[neighbor] = newCost;
                            _cameFrom[neighbor] = currentIndex;

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
            public AgentJob CurrentJob;
            public int MovementPoints;
            public int MaxFuel = 200;
            public  int CurrentFuel = 100;
            private HexGrid _grid;
            private Pathfinder _pathfinder;
            public void ConsumeFuel(int ConsumedFuel) => CurrentFuel = Math.Max(0, CurrentFuel - ConsumedFuel);

            public void AssignJob(AgentJob newJob) => CurrentJob = newJob;
            public void PlanPath()
            {
                PathResult Leg1 = new PathResult();
                PathResult Leg2 = new PathResult();
                //CONTINUE WORKING ON THIS, ADD THE OTHER PATHS, DONT FORGET TO ADD WIEGHT
                // Move from current location to pick up locaton
                Leg1 = LogisticsEngine.PlanRouteWithRefuel(_grid, this, this.CurrentLocation, _grid.GetNodeByIndex(CurrentJob.PickUpIndex), _pathfinder);
                if (Leg1.Status == PathStatus.Success)
                {
                    //this.MoveAlongPath(Leg1.Path);
                    HexNode newLocationIndex = _grid.GetNodeByIndex(Leg1.Path[Leg1.Path.Count-1]);
                    // if path is valid move from pick up locaton to drop off location
                    Leg2 = LogisticsEngine.PlanRouteWithRefuel(_grid, this, newLocationIndex, _grid.GetNodeByIndex(CurrentJob.PickUpIndex), _pathfinder);
                   
                }
                if (Leg2.Status == PathStatus.Success)
                {
                    Console.WriteLine("Successful");
                }
                else
                {
                    Console.WriteLine("Failure");
                }
                Console.WriteLine($"{Leg1.Status}, {Leg2.Status}");

            }

            public void MoveAlongPath(List<int> path) => this.CurrentLocation = _grid.GetNodeByIndex(path[-1]);


            public Agent(HexNode location, HexGrid grid, Pathfinder pathfinder)
            {
                CurrentLocation = location;
                _grid = grid;
                _pathfinder = pathfinder;
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

        public struct TerrainType //IMPLEMENT ENUMS, DIDNT KNOW ABOUT THEM A MONTH AGO SO FORGOT
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
            public static PathResult PlanRouteWithRefuel(HexGrid grid, Agent agent, HexNode start, HexNode destination, Pathfinder pathfinder) //DOES NOT WORK EFFICIENTLY. REFACTOR LOGIC ASAP
            {
                //  Try a direct path first (Ignore fuel constraint to see total cost)
                HexNode agentLocation = start;
                PathResult direct = pathfinder.FindPath(grid, agentLocation, destination, agent.CurrentFuel);
                if (direct.Status == PathStatus.Success) return direct;
                //  If too expensive, look for an Oasis
                List<int> stations = grid.GetRefuelStationIndices();
                int bestTotalCost = int.MaxValue;
                PathResult bestLeg1 = new PathResult();
                PathResult bestLeg2 = new PathResult();
                PathResult Final = new PathResult();
                PathResult Leg1 = new PathResult();
                PathResult Leg2 = new PathResult();
                HexNode stationNode;
                foreach (int stationIdx in stations)
                {
                    // Apply Geometric Pre-Filter here (Distance checks)
                    stationNode = grid.GetNodeByIndex(stationIdx);
                    int distToStation = agentLocation.Coords.GetDistance(stationNode.Coords);
                    // If even a straight line of Plains is too expensive, skip this station.
                    if (distToStation * grid.MinCost > agent.CurrentFuel) continue;

                    // TODO: If it passes pre-filter, Calculate Leg 1 and Leg 2
                   
                    Leg1 = pathfinder.FindPath(grid, agentLocation, stationNode, agent.CurrentFuel);
                    Leg2 = pathfinder.FindPath(grid, stationNode, destination, agent.MaxFuel);
                    if (Leg1.Status == PathStatus.Success && Leg2.Status == PathStatus.Success) //if path is possible
                    {
                        Console.WriteLine("found viable path");
                        if (Leg1.TotalCost <= agent.CurrentFuel && Leg2.TotalCost <= agent.MaxFuel)// if path is within fuel limit
                        {
                            if (Leg1.TotalCost + Leg2.TotalCost < bestTotalCost) // compare best and store
                            {
                                bestLeg1 = Leg1;
                                bestLeg2 = Leg2;
                                bestTotalCost = bestLeg1.TotalCost + bestLeg2.TotalCost;
                                Console.WriteLine("New best path");
                            }
                        }
                    }
                    
                }

                //  Combine Leg 1 and Leg 2 into a single PathResult or return a 'failure'
                if (bestLeg1.Path != null && bestLeg2.Path != null)
                {
                    Final.Path.AddRange(bestLeg1.Path);
                    Final.Path.AddRange(bestLeg2.Path.Skip(1));
                    Final.Status = PathStatus.Success;
                    Final.TotalCost = bestTotalCost;
                }
                else
                {
                    Final.Status = PathStatus.Unreachable;
                }

                    return Final;
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


