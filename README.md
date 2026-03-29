# 🗺️ Hex Grid Logistics System

> A procedurally generated hexagonal map engine with A\* pathfinding, weighted terrain traversal, and a modular logistics cost calculator — built in C#.

---

## 📌 Elevator Pitch

This project simulates the core of a turn-based logistics or strategy game engine. An agent navigates a randomly generated hex map — dodging impassable mountains, wading through costly swamps — using A\* to find the optimal route. A dedicated `LogisticsEngine` then calculates the total fuel cost of any given path. The architecture is cleanly separated: grid generation, pathfinding, agent movement, and cost analysis are all independent, composable systems.

---

## ⚙️ Technical Depth

### 1. Cube Coordinate System

Hex grids are represented using **three-axis cube coordinates** `(Q, R, S)` with the hard constraint:

```
Q + R + S = 0
```

This constraint is enforced at the struct level via a constructor guard:

```csharp
if (q + r + s != 0)
    throw new ArgumentException("Sum of Hex coordinates (q+r+s) must be 0.");
```

The elegance of this system is that **distance between two hexes** becomes a simple arithmetic operation:

```csharp
return (Math.Abs(Q - target.Q) + Math.Abs(R - target.R) + Math.Abs(S - target.S)) / 2;
```

And all **6 neighbours** are pre-defined as direction vectors, making neighbour lookup O(1):

```csharp
public static HexCoords[] Directions = {
    new HexCoords(1, -1, 0), new HexCoords(1, 0, -1), new HexCoords(0, 1, -1),
    new HexCoords(-1, 1, 0), new HexCoords(-1, 0, 1), new HexCoords(0, -1, 1)
};
```

---

### 2. A\* Pathfinding on a Weighted Graph

The `Pathfinder` class implements the **A\* search algorithm** — a best-first search that combines:

- **g(n)**: the actual movement cost accumulated from the start node
- **h(n)**: a heuristic estimate of the remaining cost to the goal (here, the hex distance)

The key scoring logic:

```csharp
HexNode current = openSet.OrderBy(n => costSoFar[n] + Heuristic(n, end)).First();
```

`f(n) = g(n) + h(n)` ensures the algorithm always expands the most *promising* node first. Because the heuristic uses the true geometric hex distance (which never *overestimates* the real cost), A\* is **admissible** here — it is guaranteed to find the optimal path.

Path reconstruction works by walking back through the `cameFrom` dictionary from end → start, then reversing.

---

### 3. Terrain & Movement Cost Model

Each `HexNode` carries a `TerrainType` with two properties:

| Terrain  | Movement Cost | Passable |
|----------|---------------|----------|
| Plains   | 1             | ✅ Yes   |
| Swamp    | 5             | ✅ Yes   |
| Mountain | 1 (irrelevant)| ❌ No    |

A\* accounts for terrain cost via `neighbor.MovementCost` when updating `costSoFar`, so the pathfinder naturally prefers plains over swamps when they produce a lower total cost path — not just a shorter one.

---

### 4. Procedural Map Generation

The `GenerateMap(int radius)` method uses **axial ring iteration** to generate every valid hex within a given radius:

```csharp
for (int q = -radius; q <= radius; q++)
    for (int r = Math.Max(-radius, -q - radius); r <= Math.Min(radius, -q + radius); r++)
```

This loop bounds `r` relative to `q`, visiting every coordinate exactly once. Terrain is assigned stochastically: 20% swamp probability, 10% mountain probability, remainder plains.

---

### 5. Logistics Engine

The `LogisticsEngine` separates **cost analysis** from movement, using LINQ for a clean, declarative sum:

```csharp
return path.Skip(1).Sum(node => node.MovementCost);
```

`Skip(1)` is intentional: you don't pay a movement cost for the tile you're already standing on.

---

## 🚀 Installation & Usage

### Prerequisites

- [.NET SDK 6+](https://dotnet.microsoft.com/download)
- Any IDE: Visual Studio, Rider, or VS Code with the C# extension

### Running the Project

```bash
git clone https://github.com/YOUR_USERNAME/hex-grid-logistics-system.git
cd hex-grid-logistics-system
dotnet run
```

### What to Expect

The console will:
1. Generate a hex map of radius 5
2. Print the 6 neighbours of the centre tile
3. Attempt to pathfind from `(0,0,0)` to `(5,-3,-2)`
4. Print the full path as a list of cube coordinates, or report if the destination is blocked/unreachable

### Customising the Map

Modify the `radius` variable in `Main` to scale the map, or adjust the probability thresholds in `GenerateMap` to change terrain density:

```csharp
int radius = 5; // Change map size here

if (SwampChance < 20) { ... }  // Adjust swamp frequency
else if (MountainChance < 10) { ... }  // Adjust mountain frequency
```

---

## 🎓 Learning Outcomes

Building this project developed mastery of the following:

- **Cube Coordinate Geometry** — understanding why the `Q+R+S=0` constraint makes hex maths elegant and how to derive neighbour vectors from first principles
- **A\* Algorithm** — implementing heuristic search from scratch, understanding admissibility, and using a `cameFrom` dictionary for path reconstruction
- **Graph Theory in Practice** — modelling a tile map as a weighted, directed graph and traversing it efficiently using a dictionary-backed open set
- **Separation of Concerns** — decoupling the grid, pathfinder, agent, and logistics engine into independent classes with clean interfaces
- **C# Data Structures** — leveraging `Dictionary<K,V>` for O(1) node lookup, `struct` for lightweight value-type coordinates, and LINQ for expressive cost aggregation
- **Procedural Generation** — generating a spatially coherent map using bounded axial iteration with randomised terrain assignment

---

## 🏷️ Suggested GitHub Topics

See the tags section below.
