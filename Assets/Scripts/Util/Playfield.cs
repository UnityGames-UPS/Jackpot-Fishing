using UnityEngine;

// Maps between server space (1920x1080 px, origin top-left, y down) and the scene.
// The playfield RectTransform (Parent1) is a fixed 1920x1080 rect with a centered pivot,
// so one server pixel == one local unit. Its rotation (portrait) and canvas scaling are
// handled by TransformPoint, so nothing else needs to know about them.
internal static class Playfield
{
  internal const float Width = 1920f;
  internal const float Height = 1080f;

  private static RectTransform space;

  internal static bool IsReady => space != null;

  internal static void Init(RectTransform playfieldRect)
  {
    space = playfieldRect;
  }

  internal static Vector2 ServerToLocal(Vector2 server) =>
    new Vector2(server.x - Width * 0.5f, Height * 0.5f - server.y);

  internal static Vector2 LocalToServer(Vector2 local) =>
    new Vector2(local.x + Width * 0.5f, Height * 0.5f - local.y);

  internal static Vector2 ServerDirToLocal(Vector2 dir) => new Vector2(dir.x, -dir.y);

  internal static Vector3 ServerToWorld(Vector2 server) =>
    space.TransformPoint(ServerToLocal(server));

  internal static Vector3 ServerDirToWorld(Vector2 dir) =>
    space.TransformDirection(ServerDirToLocal(dir));

  internal static Vector2 WorldToServer(Vector3 world) =>
    LocalToServer(space.InverseTransformPoint(world));

  internal static Vector2 WorldDirToServer(Vector3 worldDir)
  {
    Vector3 local = space.InverseTransformDirection(worldDir);
    return new Vector2(local.x, -local.y);
  }
}
