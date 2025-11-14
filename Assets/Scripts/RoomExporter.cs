using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoomDimensions
{
    public float height;
    public string unit = "meters";
}

[System.Serializable]
public class Wall
{
    public string from;
    public string to;
    public float distance;
    public string direction;
}

[System.Serializable]
public class Corner
{
    public string id;
    public Vector2 position;

    public Corner(string id, Vector3 worldPosition)
    {
        this.id = id;
        this.position = new Vector2(worldPosition.x, worldPosition.z);
    }

    public Corner() { }
}

[System.Serializable]
public class Obstacle
{
    public string type;
    public string id;
    public ObstacleDimensions dimensions;
    public List<string> attached_to;
}

[System.Serializable]
public class ObstacleDimensions
{
    public float width;
    public float depth;
    public float height;
}

[System.Serializable]
public class RoomData
{
    public RoomDimensions room_dimensions;
    public List<Corner> corners;
    public List<Wall> walls;
    public List<Obstacle> obstacles;
    public string origin_reference = "A";
}
