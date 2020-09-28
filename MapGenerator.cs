using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    public int width, height;
    [Range(0,100)]
    public int randomFillPercent;
    public int borderSize = 1;

    public string seed;
    public bool useRandomSeed;
    private int[,] map;
    private int[,] type;

    private GameObject mapContainer;
    
    public int smoothNumber = 5;
    public bool processWallRegions = true;
    public bool processFloorRegions = true;
    public bool smoothMap = true;
    public int minRegionSize;
    public int passagewaysRadius = 1;

    public Tilemap tileMap;
    public Tile[] tiles;

    private void Start()
    {
        GenerateMap();
    }

    private void Update()
    {
        if(Input.GetButton("Fire2"))
        {
            GenerateMap();
        }
    }
    private void GenerateMap()
    {
        map = new int[width, height];
        RandomFillMap();
        if (smoothMap)
        {
            for (int i = 0; i < smoothNumber; i++)
            {
                SmoothMap();
            }
        }
        ProcessMap();
        CreateBorders();

        DrawTiles(map, tileMap, tiles);
    }

    private void CreateBorders()
    {
        int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];
        for (int x = 0; x<borderedMap.GetLength(0); x++)
        {
            for (int y = 0; y<borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x<width + borderSize && y >= borderSize && y<height + borderSize)
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else
                {
                    borderedMap[x, y] = 1;
                }
            }
        }
    }

    // Draw the tile on the tilemap
    private void DrawTiles(int[,] map, Tilemap tileMap, TileBase[] tile)
    {
        tileMap.ClearAllTiles();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int position = new Vector3Int(x, y, 0);
                if (map[x, y] == 0)
                {
                    tileMap.SetTile(position, tile[0]);
                }
                else
                {
                    tileMap.SetTile(position, tile[1]);
                }
            }
        }
    }

    // Delete region with a side < minRegionSide
    private void ProcessMap()
    {
        if (processWallRegions)
        {
            List<List<Coord>> wallRegions = GetRegions(1);
            
            foreach (List<Coord> region in wallRegions)
            {
                if (region.Count < minRegionSize)
                {
                    foreach (Coord tile in region)
                    {
                        map[tile.tileX, tile.tileY] = 0;
                    }
                }
            }
        }

        if (processFloorRegions)
        {
            List<List<Coord>> floorRegions = GetRegions(0);

            List<Room> rooms = new List<Room>();
            foreach (List<Coord> region in floorRegions)
            {
                if (region.Count < minRegionSize)
                {
                    foreach (Coord tile in region)
                    {
                        map[tile.tileX, tile.tileY] = 1;
                    }
                }
                else
                {
                    rooms.Add(new Room(region,map));
                }
            }
            rooms.Sort();
            rooms[0].isMainRoom = true;
            rooms[0].isAccessibleFromMainRoom = true;
            ConnectClosestRooms(rooms);
        }
    }

    private void ConnectClosestRooms(List<Room> rooms,bool forceAccessibilityFromMainRoom = false)
    {
        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();

        if(forceAccessibilityFromMainRoom)
        {
            foreach(Room room in rooms)
            {
                if(room.isAccessibleFromMainRoom)
                {
                    roomListB.Add(room);
                }
                else
                {
                    roomListA.Add(room);
                }
            }
        }
        else
        {
            roomListA = rooms;
            roomListB = rooms;
        }
        int bestDistance = 0;
        Coord bestTileA = new Coord();
        Coord bestTileB = new Coord();

        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach(Room roomA in roomListA)
        {
            if(!forceAccessibilityFromMainRoom)
            {
                possibleConnectionFound = false;
                if(roomA.connectedRooms.Count > 0)
                {
                    continue;
                }
            }
            foreach (Room roomB in roomListB)
            {
                if (roomA == roomB || roomA.IsConnected(roomB))
                {
                    continue;
                }
                for (int indexA = 0; indexA < roomA.edgeTiles.Count; indexA++)
                {
                    for (int indexB = 0; indexB < roomB.edgeTiles.Count; indexB++)
                    {
                        Coord A = roomA.edgeTiles[indexA];
                        Coord B = roomB.edgeTiles[indexB];

                        int distanceBetweenRooms = (int)(Mathf.Pow(A.tileX - B.tileX, 2) + Mathf.Pow(A.tileY - B.tileY, 2));
                        if(distanceBetweenRooms < bestDistance || !possibleConnectionFound)
                        {
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = A;
                            bestTileB = B;
                            bestRoomA = roomA;
                            bestRoomB = roomB;

                        }
                    }
                }
            }
            if(possibleConnectionFound && !forceAccessibilityFromMainRoom)
            {
                CreatePassage(bestTileA, bestTileB, bestRoomA, bestRoomB);
            }
        }
        if (possibleConnectionFound && forceAccessibilityFromMainRoom)
        {
            CreatePassage(bestTileA, bestTileB, bestRoomA, bestRoomB);
            ConnectClosestRooms(rooms, true);
        }
        if (!forceAccessibilityFromMainRoom)
        {
            ConnectClosestRooms(rooms,true);
        }
    }

    private void CreatePassage(Coord tileA,Coord tileB,Room roomA, Room roomB)
    {
        Room.ConnectRoom(roomA, roomB);
        List<Coord> line = GetLine(tileA, tileB);
        Debug.DrawLine(CoordToWorldPosition(tileA), CoordToWorldPosition(tileB), Color.green, 100);
        foreach(Coord tile in line)
        {
            DrawInCircle(tile, passagewaysRadius);
        }
    }

    private void DrawInCircle(Coord tile,int radius)
    {
        for(int x = -radius; x <= radius; x++)
        {
            for(int y = -radius; y <= radius; y++)
            {
                if(x*x + y*y <= radius * radius)
                {
                    int drawX = tile.tileX + x;
                    int drawY = tile.tileY + y;
                    if(MapIsInRange(drawX,drawY))
                    {
                        map[drawX, drawY] = 0;
                    }
                }
            }
        }
    }

    private List<Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;
        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if (longest < shortest)
        {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));

            if (inverted)
            {
                y += step;
            }
            else
            {
                x += step;
            }

            gradientAccumulation += shortest;
            if (gradientAccumulation >= longest)
            {
                if (inverted)
                {
                    x += gradientStep;
                }
                else
                {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    private Vector3 CoordToWorldPosition(Coord coord)
    {
        return new Vector3(-width / 2 + .5f + coord.tileX, -height / 2 + .5f + coord.tileY,2);
    }
    private List<List<Coord>> GetRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if(mapFlags[x,y] == 0 && map[x,y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);
                    foreach(Coord coord in newRegion)
                    {
                        mapFlags[coord.tileX, coord.tileY] = 1;
                    }
                }
            }
        }

        return regions;
    }

    // Retourne une liste de coordonnées prochent formant une région
    private List<Coord> GetRegionTiles(int startX, int startY)
    {

        List<Coord> coords = new List<Coord>();
        int[,] mapFlags = new int[width, height];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;
        while(queue.Count > 0)
        {
            Coord coord = queue.Dequeue();
            coords.Add(coord);

            for(int x = coord.tileX -1; x <= coord.tileX +1; x++)
            {
                for(int y = coord.tileY -1; y <= coord.tileY +1; y++)
                {
                    if(MapIsInRange(x,y) && (y == coord.tileY || x == coord.tileX))
                    {
                        if(mapFlags[x,y] == 0 && map[x,y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }

        return coords;
    }

    private bool MapIsInRange(int x,int y)
    {
        return (x >= 0 && x < width) && (y >= 0 && y < height);
    }

    private void RandomFillMap()
    {
        if(useRandomSeed)
        {
            seed = Time.time.ToString();
        }

        System.Random randomNumber = new System.Random(seed.GetHashCode());
        for(int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (randomNumber.Next(0, 100) < randomFillPercent) ? 1 : 0;
                } 
            }
        }
    }

    private void SmoothMap()
    {
        for(int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int WallsAround = GetAroundWallCount(x, y);
                if(WallsAround > 4)
                {
                    map[x, y] = 1;
                }
                else if(WallsAround < 4)
                {
                    map[x, y] = 0;
                }
            }
        }
    }

    private int GetAroundWallCount(int x,int y)
    {
        int counter = 0;
        for(int xAround = x -1; xAround <=  x + 1; xAround++)
        {
            for(int yAround = y -1; yAround <= y + 1; yAround++)
            {
                if (MapIsInRange(xAround,yAround))
                {
                    if (xAround != x || yAround != y)
                    {
                        counter += map[xAround, yAround];
                    }
                }
                else
                {
                    counter++;
                }
            }
        }
        return counter;
    }

    private struct Coord
    {
        public int tileX;
        public int tileY;

        public Coord(int x,int y)
        {
            tileX = x;
            tileY = y;
        }
    }

    
    private class Room : IComparable<Room>
    {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isMainRoom;
        public bool isAccessibleFromMainRoom;

        public Room()
        {

        }

        public Room(List<Coord> _tiles, int[,] map)
        {
            tiles = _tiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();
            edgeTiles = new List<Coord>();
            foreach (Coord coord in tiles)
            {
                for (int x = coord.tileX - 1; x <= coord.tileX + 1; x++)
                {
                    for (int y = coord.tileY - 1; y <= coord.tileY + 1; y++)
                    {
                        if(x == coord.tileX || y == coord.tileY)
                        {
                            if(map[x,y] == 1)
                            {
                                edgeTiles.Add(coord);
                            }
                        }
                    }
                }
            }
        }

        public void SetAccessibleFromMainRoom()
        {
            if(!isAccessibleFromMainRoom)
            {
                isAccessibleFromMainRoom = true;
                foreach(Room connectedRoom in connectedRooms)
                {
                    connectedRoom.isAccessibleFromMainRoom = true;
                }
            }
        }
        public static void ConnectRoom(Room A, Room B)
        {
            if(A.isAccessibleFromMainRoom)
            {
                B.SetAccessibleFromMainRoom();
            }
            else if(B.isAccessibleFromMainRoom)
            {
                A.SetAccessibleFromMainRoom();
            }
            A.connectedRooms.Add(B);
            B.connectedRooms.Add(A);
        }

        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }

        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }
    }

}
