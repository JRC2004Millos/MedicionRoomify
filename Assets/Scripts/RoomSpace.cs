using System.Collections.Generic;
using UnityEngine;

public class RoomSpace : MonoBehaviour
{
    [Header("Bounds (mundo)")]
    public float minX, maxX, minZ, maxZ;
    public float floorY = 0.05f;

    [Header("Root del cuarto (padre de piso, muros y objetos)")]
    public Transform roomRoot;

    [Header("Polígono real del piso (mundo XZ, CCW)")]
    [SerializeField] private List<Vector2> floorPolygonXZ = new List<Vector2>();

    [Header("Debug")]
    public bool drawBounds = true;
    public bool drawPolygon = true;
    public Color boundsColor = new Color(0f, 0.8f, 1f, 0.2f);
    public Color polyColor = new Color(0.1f, 1f, 0.2f, 0.35f);

    public void SetFloorPolygonWorldXZ(List<Vector2> worldPoly)
    {
        floorPolygonXZ = worldPoly ?? new List<Vector2>();
    }

    public bool HasValidBounds()
    {
        return !(float.IsNaN(minX) || float.IsNaN(maxX) || float.IsNaN(minZ) || float.IsNaN(maxZ))
               && maxX > minX && maxZ > minZ;
    }

    public bool HasPolygon() => floorPolygonXZ != null && floorPolygonXZ.Count >= 3;

    public bool ContainsXZ(Vector3 worldPos)
    {
        if (!HasPolygon()) return worldPos.x >= minX && worldPos.x <= maxX && worldPos.z >= minZ && worldPos.z <= maxZ;
        return PointInPolygon(new Vector2(worldPos.x, worldPos.z), floorPolygonXZ);
    }

    public Vector3 ClampWorldToInside(Vector3 worldPos)
    {
        worldPos.y = floorY;

        if (HasPolygon())
        {
            if (PointInPolygon(new Vector2(worldPos.x, worldPos.z), floorPolygonXZ))
                return worldPos;

            Vector2 p = new Vector2(worldPos.x, worldPos.z);
            Vector2 closest = ClosestPointOnPolygon(p, floorPolygonXZ);
            return new Vector3(closest.x, floorY, closest.y);
        }
        else
        {
            float x = Mathf.Clamp(worldPos.x, minX, maxX);
            float z = Mathf.Clamp(worldPos.z, minZ, maxZ);
            return new Vector3(x, floorY, z);
        }
    }

    private static bool PointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];
            bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                             (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + Mathf.Epsilon) + pi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + Mathf.Epsilon);
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }

    private static Vector2 ClosestPointOnPolygon(Vector2 p, List<Vector2> poly)
    {
        float bestDist = float.PositiveInfinity;
        Vector2 best = p;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Count];
            Vector2 q = ClosestPointOnSegment(a, b, p);
            float d = (q - p).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = q; }
        }
        return best;
    }
}
