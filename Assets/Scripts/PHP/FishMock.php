import React, { useEffect, useState, useRef, useCallback } from "react";
import { io, Socket } from "socket.io-client";
import { config } from "@/lib/config";
import { useTransitionNavigate } from "@/hooks/useTransitionNavigate";
import { useUiStore } from "@/store/ui";

const FishEvents = {
  CLIENT: {
    REQUEST: "request",
  },
  SERVER: {
    INIT: "game:init",
    BALANCE_SYNC: "balance:sync",
    FISH_SPAWNED: "game:fish_spawned",
    FISH_DESPAWNED: "game:fish_despawned",
    SHOT_RESULT: "game:shot_result",
    FORMATION_ALERT: "game:formation_alert",
    BULLET_FIRED: "game:bullet_fired",
    ERROR: "error",
  },
} as const;

const SHOT_OUTCOME = {
  MISS_GEOMETRY: "MISS_GEOMETRY",
  MISS_GATE: "MISS_GATE",
  KILL: "KILL",
} as const;

interface FishGameDummyProps {
  gameinfo: string;
  gameslug: string;
}

interface LogEntry {
  id: number;
  time: string;
  type: "IN" | "OUT" | "INFO" | "ERROR";
  event: string;
  payload: any;
}

export interface SplinePathParams {
  points: { x: number; y: number; z: number }[];
  cumulativeLengths: number[];
}

export type MLFGPathParamsMapDummy = {
  spline: SplinePathParams;
};

export interface BaseSpawnedFishDummy<T extends keyof MLFGPathParamsMapDummy> {
  id: string;
  type: string;
  variant: string;
  pathType: T;
  pathParams: MLFGPathParamsMapDummy[T];
  speed: number;
  startPosition: { x: number; y: number; z: number };
  endPosition: { x: number; y: number; z: number };
  spawnTime: number;
  lifespan: number;
  hitboxRadius: number;
  multiplier: number;
  hitChance: number;
  motionKeyframes?: { t: number; d: number }[]; // Pre-computed time→distance table
}

export type SpawnedFish = BaseSpawnedFishDummy<"spline">;

/**
 * Given elapsed time (ms) and a pre-computed motionKeyframes table,
 * returns cumulative distance traveled (px) via linear interpolation.
 * Both server and client use this exact function — guaranteed sync.
 */
export function getDistanceFromKeyframes(
  elapsedMs: number,
  keyframes: { t: number; d: number }[],
): number {
  if (keyframes.length === 0) return 0;

  if (elapsedMs <= 0) {
    if (keyframes.length >= 2) {
      const a = keyframes[0];
      const b = keyframes[1];
      if (b.t > a.t) {
        const ratio = (elapsedMs - a.t) / (b.t - a.t);
        return a.d + ratio * (b.d - a.d);
      }
    }
    return keyframes[0].d;
  }

  const last = keyframes[keyframes.length - 1];
  if (elapsedMs >= last.t) {
    if (keyframes.length >= 2) {
      const a = keyframes[keyframes.length - 2];
      const b = keyframes[keyframes.length - 1];
      if (b.t > a.t) {
        const ratio = (elapsedMs - a.t) / (b.t - a.t);
        return a.d + ratio * (b.d - a.d);
      }
    }
    return last.d;
  }

  for (let i = 0; i < keyframes.length - 1; i++) {
    const a = keyframes[i];
    const b = keyframes[i + 1];
    if (elapsedMs >= a.t && elapsedMs < b.t) {
      const ratio = (elapsedMs - a.t) / (b.t - a.t);
      return a.d + ratio * (b.d - a.d);
    }
  }
  return last.d;
}

export function getSplinePositionAt(
  points: { x: number; y: number; z: number }[],
  cumulativeLengths: number[],
  t: number,
) {
  if (points.length === 0) return { x: 0, y: 0, z: 0 };
  if (points.length === 1) return points[0];

  const totalLength = cumulativeLengths[cumulativeLengths.length - 1];
  if (totalLength <= 0) return points[0];

  const targetDistance = t * totalLength;

  if (targetDistance >= totalLength && points.length >= 2) {
    const p1 = points[points.length - 2];
    const p2 = points[points.length - 1];
    const dist1 = cumulativeLengths[cumulativeLengths.length - 2];
    const dist2 = cumulativeLengths[cumulativeLengths.length - 1];
    const segmentLen = dist2 - dist1;
    if (segmentLen > 0) {
      const localT = (targetDistance - dist1) / segmentLen;
      return {
        x: p1.x + (p2.x - p1.x) * localT,
        y: p1.y + (p2.y - p1.y) * localT,
        z: p1.z + (p2.z - p1.z) * localT,
      };
    }
    return points[points.length - 1];
  }

  if (targetDistance <= 0 && points.length >= 2) {
    const p1 = points[0];
    const p2 = points[1];
    const dist1 = cumulativeLengths[0];
    const dist2 = cumulativeLengths[1];
    const segmentLen = dist2 - dist1;
    if (segmentLen > 0) {
      const localT = (targetDistance - dist1) / segmentLen;
      return {
        x: p1.x + (p2.x - p1.x) * localT,
        y: p1.y + (p2.y - p1.y) * localT,
        z: p1.z + (p2.z - p1.z) * localT,
      };
    }
    return points[0];
  }

  let low = 0;
  let high = cumulativeLengths.length - 1;

  while (low <= high) {
    const mid = Math.floor((low + high) / 2);
    if (cumulativeLengths[mid] < targetDistance) {
      low = mid + 1;
    } else {
      high = mid - 1;
    }
  }

  const i = Math.max(0, low - 1);
  const nextI = Math.min(i + 1, points.length - 1);

  if (i === nextI) return points[i];

  const segmentStartDist = cumulativeLengths[i];
  const segmentEndDist = cumulativeLengths[nextI];
  const segmentLen = segmentEndDist - segmentStartDist;

  let localT = 0;
  if (segmentLen > 0) {
    localT = (targetDistance - segmentStartDist) / segmentLen;
  }

  const p1 = points[i];
  const p2 = points[nextI];

  return {
    x: p1.x + (p2.x - p1.x) * localT,
    y: p1.y + (p2.y - p1.y) * localT,
    z: p1.z + (p2.z - p1.z) * localT,
  };
}

let nextLogId = 1;

const FishGameDummy: React.FC<FishGameDummyProps> = ({
  gameinfo,
  gameslug,
}) => {
  const navigate = useTransitionNavigate();

  useEffect(() => {
    useUiStore.getState().setGameLoadProgress(100);
    useUiStore.getState().setPanIndex(0);
    useUiStore.getState().setIsGameActive(true);

    const prevBg = document.body.style.backgroundColor;
    document.body.style.backgroundColor = "#050e1d";

    return () => {
      useUiStore.getState().setIsGameActive(false);
      document.body.style.backgroundColor = prevBg;
    };
  }, []);
  const [socket, setSocket] = useState<Socket | null>(null);
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [balance, setBalance] = useState<number | null>(null);
  const [roomId, setRoomId] = useState<string>("Unknown");
  const [weapon, setWeapon] = useState<string>("normal");
  const [tideWarningText, setTideWarningText] = useState<string | null>(null);

  const [clockOffset, setClockOffset] = useState<number>(0);
  const [serverTime, setServerTime] = useState<number>(Date.now());

  const [seatIndex, setSeatIndex] = useState<number>(-1);
  const [maxBulletBounces, setMaxBulletBounces] = useState<number>(3);
  const [roomSeats, setRoomSeats] = useState<Record<string, number>>({});
  const [gameDataSeats, setGameDataSeats] = useState<
    { x: number; y: number; z: number }[]
  >([]);

  const [activeFish, setActiveFish] = useState<SpawnedFish[]>([]);
  const [mousePos, setMousePos] = useState({ x: 960, y: 540 });
  const canvasRef = useRef<HTMLCanvasElement>(null);

  const logsContainerRef = useRef<HTMLDivElement>(null);
  const [autoScroll, setAutoScroll] = useState(true);
  const [showFishLogs, setShowFishLogs] = useState(false);

  const addLog = useCallback(
    (type: "IN" | "OUT" | "INFO" | "ERROR", event: string, payload?: any) => {
      setLogs((prev) => {
        const newLogs = [
          ...prev,
          {
            id: nextLogId++,
            time: new Date().toLocaleTimeString(),
            type,
            event,
            payload,
          },
        ];
        if (newLogs.length > 200) return newLogs.slice(newLogs.length - 200);
        return newLogs;
      });
    },
    [],
  );

  useEffect(() => {
    if (autoScroll && logsContainerRef.current) {
      const container = logsContainerRef.current;
      container.scrollTop = container.scrollHeight;
    }
  }, [logs, autoScroll]);

  // Clock Sync Logic
  useEffect(() => {
    if (!socket) return;

    let syncAttempts = 0;
    const doSync = () => socket.emit("time_sync", Date.now());

    const burstInterval = setInterval(() => {
      if (syncAttempts < 5) {
        doSync();
        syncAttempts++;
      } else {
        clearInterval(burstInterval);
      }
    }, 200);

    const periodicInterval = setInterval(() => {
      doSync();
    }, 5000);

    const onTimeSyncAck = (data: {
      clientTime?: number;
      serverTime: number;
    }) => {
      if (data.clientTime) {
        const latency = (Date.now() - data.clientTime) / 2;
        const offset = data.serverTime - Date.now() + latency;
        setClockOffset(offset);
      }
    };

    socket.on("time_sync_ack", onTimeSyncAck);

    return () => {
      clearInterval(burstInterval);
      clearInterval(periodicInterval);
      socket.off("time_sync_ack", onTimeSyncAck);
    };
  }, [socket]);

  // Update Display Time
  useEffect(() => {
    const timer = setInterval(() => {
      setServerTime(Date.now() + clockOffset);
    }, 100);
    return () => clearInterval(timer);
  }, [clockOffset]);

  useEffect(() => {
    const isReload = sessionStorage.getItem("mlfg_active") === "true";
    if (isReload) {
      sessionStorage.removeItem("mlfg_active");
      // Add a slight delay before navigating back to avoid any React Router race conditions
      setTimeout(() => navigate("/"), 0);
      return;
    }

    sessionStorage.setItem("mlfg_active", "true");

    let gameToken = "";
    let socketUrl = config.realtimeServer;

    try {
      const urlObj = new URL(gameinfo);
      gameToken = urlObj.searchParams.get("token") || "";
      const queryUrl = urlObj.searchParams.get("url");
      if (queryUrl) {
        socketUrl = queryUrl;
      }
    } catch {
      gameToken = gameinfo;
    }

    if (!gameToken) {
      addLog("ERROR", "No token found in gameinfo");
      return;
    }

    const namespaceUrl = `${socketUrl.replace(/\/$/, "")}/playground-multiplayer`;

    addLog("INFO", `Connecting to Realtime Server at ${namespaceUrl}`);

    const newSocket = io(namespaceUrl, {
      auth: { token: gameToken },
      transports: ["websocket"],
    });

    setSocket(newSocket);

    newSocket.on("connect", () => {
      addLog("INFO", "Connected to socket", { socketId: newSocket.id });
    });

    newSocket.on("room:joined", (data: any) => {
      const parsed = typeof data === "string" ? JSON.parse(data) : data;
      addLog("IN", "room:joined", parsed);
      if (parsed?.roomId) {
        setRoomId(parsed.roomId);
      }
    });

    newSocket.on("disconnect", (reason) => {
      addLog("ERROR", "Disconnected", { reason });
      if (reason === "io server disconnect") {
        setTimeout(() => newSocket.connect(), 500);
      }
    });

    newSocket.on("connect_error", (err) => {
      addLog("ERROR", "Connection Error", err.message);
    });

    newSocket.on(FishEvents.SERVER.INIT, (data) => {
      const parsed = typeof data === "string" ? JSON.parse(data) : data;
      addLog("IN", FishEvents.SERVER.INIT, parsed);
      const balance =
        parsed?.player?.balance ?? parsed?.payload?.player?.balance;
      if (balance !== undefined) setBalance(balance);

      // Save local userId so we can filter SHOT_RESULT and BULLET_FIRED events
      const localUserId =
        parsed?.player?.userId ?? parsed?.payload?.player?.userId;
      if (localUserId) myUserIdRef.current = localUserId;

      const parsedRoomId = parsed?.roomId ?? parsed?.payload?.roomId;
      if (parsedRoomId) setRoomId(parsedRoomId);

      const activeFish = parsed?.activeFish ?? parsed?.payload?.activeFish;
      if (activeFish) setActiveFish(activeFish);

      const parsedSeatIndex = parsed?.seatIndex ?? parsed?.payload?.seatIndex;
      if (parsedSeatIndex !== undefined) setSeatIndex(parsedSeatIndex);

      const parsedRoomSeats = parsed?.roomSeats ?? parsed?.payload?.roomSeats;
      if (parsedRoomSeats) setRoomSeats(parsedRoomSeats);

      const parsedSeats =
        parsed?.gameData?.seats ?? parsed?.payload?.gameData?.seats;
      if (parsedSeats) setGameDataSeats(parsedSeats);

      const parsedMaxBounces =
        parsed?.gameData?.maxBulletBounces ??
        parsed?.payload?.gameData?.maxBulletBounces;
      if (parsedMaxBounces !== undefined) setMaxBulletBounces(parsedMaxBounces);
    });

    newSocket.on(FishEvents.SERVER.BALANCE_SYNC, (data) => {
      const parsed = typeof data === "string" ? JSON.parse(data) : data;
      addLog("IN", FishEvents.SERVER.BALANCE_SYNC, parsed);
      const balance = parsed?.balance ?? parsed?.payload?.balance;
      if (balance !== undefined) setBalance(balance);
    });

    newSocket.on(FishEvents.SERVER.FISH_SPAWNED, (data) => {
      const parsed = typeof data === "string" ? JSON.parse(data) : data;
      addLog("IN", FishEvents.SERVER.FISH_SPAWNED, parsed);
      const fishData = parsed?.fish ?? parsed?.payload?.fish;
      if (fishData) {
        if (Array.isArray(fishData)) {
          setActiveFish((prev) => [...prev, ...fishData]);
        } else {
          setActiveFish((prev) => [...prev, fishData]);
        }
      }
    });

    newSocket.on(FishEvents.SERVER.FISH_DESPAWNED, (data) => {
      const parsed = typeof data === "string" ? JSON.parse(data) : data;
      addLog("IN", FishEvents.SERVER.FISH_DESPAWNED, parsed);
      const fishIds = parsed?.fishIds ?? parsed?.payload?.fishIds;
      if (fishIds)
        setActiveFish((prev) => prev.filter((f) => !fishIds.includes(f.id)));
    });

    newSocket.on(FishEvents.SERVER.SHOT_RESULT, (data) => {
      const parsed = typeof data === "string" ? JSON.parse(data) : data;
      addLog("IN", FishEvents.SERVER.SHOT_RESULT, parsed);

      const payload = parsed?.payload || parsed;

      if (payload?.shooterId && payload?.origin) {
        setRoomSeats((prev) => {
          if (prev[payload.shooterId] !== undefined) return prev;

          const origins =
            gameDataSeatsRef.current.length > 0
              ? gameDataSeatsRef.current
              : [
                  { x: 480, y: 1080 },
                  { x: 1440, y: 1080 },
                  { x: 480, y: 0 },
                  { x: 1440, y: 0 },
                ];

          const seatIdx = origins.findIndex(
            (o) => o.x === payload.origin.x && o.y === payload.origin.y,
          );
          if (seatIdx !== -1) {
            return { ...prev, [payload.shooterId]: seatIdx };
          }
          return prev;
        });
      }

      if (payload?.origin && payload?.aimTarget) {
        if (payload.shooterId) {
          opponentTargetsRef.current[payload.shooterId] = payload.aimTarget;
        }

        visualShotsRef.current.push({
          id: payload.shotId || Math.random().toString(),
          origin: payload.origin,
          aimTarget: payload.aimTarget,
          timestamp: Date.now(),
          color: payload.outcome === SHOT_OUTCOME.KILL ? "#ffea00" : "#00f0ff",
          username:
            payload.shooterUsername ||
            payload.shooterId?.substring(0, 4) ||
            "Player",
        });

        if (payload.splashHits && payload.splashHits.length > 0) {
          payload.splashHits.forEach((hit: any) => {
            splashEffectsRef.current.push({
              id: Math.random().toString(),
              origin: payload.aimTarget, // originate from the dying fish
              targetFishId: hit.fishId,
              outcome: hit.outcome,
              timestamp: Date.now(),
            });
          });
        }
      }

      if (payload?.shooterBalance !== undefined) {
        // Only update balance for this client's own shots
        if (!myUserIdRef.current || payload.shooterId === myUserIdRef.current) {
          setBalance(payload.shooterBalance);
        }
      }
    });

    // Listen for opponent bullets fired (visual only)
    newSocket.on(FishEvents.SERVER.BULLET_FIRED, (data) => {
      const parsed = typeof data === "string" ? JSON.parse(data) : data;
      addLog("IN", FishEvents.SERVER.BULLET_FIRED, parsed);

      // Ignore our own bullet broadcast — local bullet is already spawned on click
      if (myUserIdRef.current && parsed.shooterId === myUserIdRef.current)
        return;

      const { origin, angle } = parsed;
      if (!origin || angle === undefined) return;

      const speed = 1200;
      const angleRad = (angle * Math.PI) / 180;
      const vx = Math.cos(angleRad) * speed;
      const vy = Math.sin(angleRad) * speed;

      // Spawn a visual bullet for the opponent (different color: cyan)
      activeBulletsRef.current.push({
        id: Math.random().toString(),
        x: origin.x,
        y: origin.y,
        vx,
        vy,
        origin,
        color: "#00e5ff",
        owner: "opponent",
        bounces: 0,
      });
    });

    newSocket.on(FishEvents.SERVER.ERROR, (data) => {
      addLog("IN", "error", typeof data === "string" ? JSON.parse(data) : data);
    });

    newSocket.on(FishEvents.SERVER.FORMATION_ALERT, (data) => {
      const parsed = typeof data === "string" ? JSON.parse(data) : data;
      addLog("IN", FishEvents.SERVER.FORMATION_ALERT, parsed);
      setTideWarningText(parsed.message);
      setTimeout(() => {
        setTideWarningText(null);
      }, 3000);
    });

    return () => {
      sessionStorage.removeItem("mlfg_active");
      addLog("INFO", "Disconnecting...");
      newSocket.disconnect();
    };
  }, [gameinfo, addLog, navigate]);

  const activeFishRef = useRef<SpawnedFish[]>([]);
  const mousePosRef = useRef({ x: 960, y: 540 });
  const visualShotsRef = useRef<any[]>([]);
  const activeBulletsRef = useRef<any[]>([]);
  const hitEffectsRef = useRef<any[]>([]);
  const splashEffectsRef = useRef<any[]>([]);
  const lastFrameTimeRef = useRef<number>(Date.now());
  const seatIndexRef = useRef<number>(-1);
  const gameDataSeatsRef = useRef<any[]>([]);
  const myUserIdRef = useRef<string | null>(null); // Tracks this client's own userId

  const roomSeatsRef = useRef<Record<string, number>>({});
  const opponentTargetsRef = useRef<Record<string, { x: number; y: number }>>(
    {},
  );

  const socketRef = useRef<Socket | null>(null);
  const roomIdRef = useRef<string>("Unknown");
  const weaponRef = useRef<string>("normal");

  useEffect(() => {
    socketRef.current = socket;
  }, [socket]);

  useEffect(() => {
    roomIdRef.current = roomId;
  }, [roomId]);

  useEffect(() => {
    weaponRef.current = weapon;
  }, [weapon]);

  useEffect(() => {
    seatIndexRef.current = seatIndex;
  }, [seatIndex]);

  useEffect(() => {
    roomSeatsRef.current = roomSeats;
  }, [roomSeats]);

  useEffect(() => {
    gameDataSeatsRef.current = gameDataSeats;
  }, [gameDataSeats]);

  useEffect(() => {
    activeFishRef.current = activeFish;
  }, [activeFish]);

  useEffect(() => {
    mousePosRef.current = mousePos;
  }, [mousePos]);

  useEffect(() => {
    let animationFrameId: number;
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const render = () => {
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      const now = Date.now();
      const dt = lastFrameTimeRef.current
        ? (now - lastFrameTimeRef.current) / 1000
        : 0.016;
      lastFrameTimeRef.current = now;

      // Ensure maxBulletBounces is accessible in render loop
      const currentMaxBounces = maxBulletBounces;

      const currentFishPositions: Record<string, { x: number; y: number }> = {};

      // Draw active fish
      activeFishRef.current.forEach((fish) => {
        const spawnTime = fish?.spawnTime || now;
        const lifespan = fish?.lifespan || 10000;
        const elapsed = now - spawnTime;

        // progress: 0→1 along path
        // If motionKeyframes: elapsed → distance → normalize by total path length
        // Otherwise: simple time-based progress
        let progress: number;
        if (fish.motionKeyframes && fish.motionKeyframes.length > 1) {
          const totalDist =
            fish.pathParams.cumulativeLengths[
              fish.pathParams.cumulativeLengths.length - 1
            ];
          const dist = getDistanceFromKeyframes(elapsed, fish.motionKeyframes);
          progress = totalDist > 0 ? dist / totalDist : 0;
        } else {
          progress = elapsed / lifespan;
        }

        const startX = fish?.startPosition?.x ?? 0;
        const startY = fish?.startPosition?.y ?? 0;
        const endX = fish?.endPosition?.x ?? 0;
        const endY = fish?.endPosition?.y ?? 0;

        // --- DEBUG RENDER: Path, Start, End, Control Point ---
        // ---- Per-species color palette for visual debugging ----
        const FISH_COLORS: Record<
          string,
          {
            body: string;
            stroke: string;
            hitbox: string;
            path: string;
            label: string;
          }
        > = {
          golden_dragon: {
            body: "rgba(255, 215, 0, 0.5)",
            stroke: "#FFD700",
            hitbox: "rgba(255, 200, 0, 0.9)",
            path: "rgba(255, 215, 0, 0.5)",
            label: "#FFD700",
          },
          shark: {
            body: "rgba(255, 120, 0, 0.4)",
            stroke: "#FF7800",
            hitbox: "rgba(255, 120, 0, 0.9)",
            path: "rgba(255, 120, 0, 0.4)",
            label: "#FF7800",
          },
          angelfish: {
            body: "rgba(180, 80, 255, 0.4)",
            stroke: "#B450FF",
            hitbox: "rgba(180, 80, 255, 0.9)",
            path: "rgba(180, 80, 255, 0.4)",
            label: "#B450FF",
          },
          clownfish: {
            body: "rgba(0, 200, 255, 0.4)",
            stroke: "#00C8FF",
            hitbox: "rgba(0, 200, 255, 0.9)",
            path: "rgba(0, 200, 255, 0.4)",
            label: "#00C8FF",
          },
          sardine: {
            body: "rgba(0, 255, 100, 0.3)",
            stroke: "#00ff66",
            hitbox: "rgba(255, 0, 0, 0.8)",
            path: "rgba(100, 255, 255, 0.4)",
            label: "white",
          },
          crab: {
            body: "rgba(255, 50, 50, 0.5)",
            stroke: "#FF3232",
            hitbox: "rgba(255, 50, 50, 0.9)",
            path: "rgba(255, 50, 50, 0.4)",
            label: "#FF3232",
          },
          tortoise: {
            body: "rgba(50, 200, 50, 0.5)",
            stroke: "#32C832",
            hitbox: "rgba(50, 200, 50, 0.9)",
            path: "rgba(50, 200, 50, 0.4)",
            label: "#32C832",
          },
        };
        const fishType = fish?.type || "sardine";
        const colors = FISH_COLORS[fishType] ?? FISH_COLORS["sardine"];

        ctx.save();
        ctx.lineWidth = 1;
        ctx.setLineDash([5, 5]);

        const points = fish.pathParams?.points || [];
        if (points.length > 0) {
          ctx.beginPath();
          ctx.moveTo(points[0].x, points[0].y);
          for (let i = 1; i < points.length; i++) {
            ctx.lineTo(points[i].x, points[i].y);
          }
          ctx.strokeStyle = colors.path;
          ctx.stroke();
        }

        ctx.fillStyle = "rgba(0, 255, 0, 0.8)";
        ctx.beginPath();
        ctx.arc(startX, startY, 4, 0, Math.PI * 2);
        ctx.fill();
        ctx.font = "10px sans-serif";
        ctx.fillText("S", startX, startY - 8);

        ctx.fillStyle = "rgba(255, 0, 0, 0.8)";
        ctx.beginPath();
        ctx.arc(endX, endY, 4, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillText("E", endX, endY - 8);
        ctx.restore();
        // --- END DEBUG RENDER ---

        let currentX = startX;
        let currentY = startY;
        let angle = 0;

        if (points.length > 0) {
          const currentPos = getSplinePositionAt(
            points,
            fish.pathParams.cumulativeLengths,
            progress,
          );
          const nextPos = getSplinePositionAt(
            points,
            fish.pathParams.cumulativeLengths,
            Math.min(progress + 0.01, 1),
          );

          currentX = currentPos.x;
          currentY = currentPos.y;
          currentFishPositions[fish.id] = { x: currentX, y: currentY };
          angle = Math.atan2(
            nextPos.y - currentPos.y,
            nextPos.x - currentPos.x,
          );
        }

        ctx.save();
        ctx.translate(currentX, currentY);
        ctx.rotate(angle);

        // Visual radius directly bound to server hitbox for accurate debugging
        const visualRadius = fish?.hitboxRadius || 40;

        // Hitbox Debug Outline — species color
        ctx.beginPath();
        ctx.arc(0, 0, visualRadius, 0, 2 * Math.PI);
        ctx.strokeStyle = colors.hitbox;
        ctx.lineWidth = fishType === "golden_dragon" ? 3 : 2;
        ctx.stroke();

        // Body (ellipse) — species color
        // Shifted slightly forward and scaled down so tail fits inside the circle
        ctx.beginPath();
        ctx.ellipse(
          visualRadius * 0.15,
          0,
          visualRadius * 0.8,
          visualRadius * 0.5,
          0,
          0,
          2 * Math.PI,
        );
        ctx.fillStyle = colors.body;
        ctx.fill();
        ctx.strokeStyle = colors.stroke;
        ctx.lineWidth = fishType === "golden_dragon" ? 3 : 2;
        ctx.stroke();

        // Tail (triangle) — same species color
        // Tail starts inside body and goes up to -0.95 (inside the circle boundary of -1.0)
        ctx.beginPath();
        ctx.moveTo(-visualRadius * 0.45, 0);
        ctx.lineTo(-visualRadius * 0.95, -visualRadius * 0.5);
        ctx.lineTo(-visualRadius * 0.95, visualRadius * 0.5);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();

        // Eye
        ctx.beginPath();
        ctx.arc(
          visualRadius * 0.55,
          -visualRadius * 0.15,
          visualRadius * 0.1,
          0,
          2 * Math.PI,
        );
        ctx.fillStyle = "white";
        ctx.fill();

        ctx.restore();

        ctx.fillStyle = colors.label;
        ctx.font =
          fishType === "golden_dragon"
            ? "bold 14px sans-serif"
            : "bold 12px sans-serif";
        ctx.textAlign = "center";
        const label =
          fish?.variant || fish?.type || fish?.id?.substring(0, 4) || "Fish";
        ctx.fillText(label, currentX, currentY + 4);

        // --- DEBUG RENDER: Fish Stats ---
        ctx.font = "10px monospace";
        ctx.fillStyle = "rgba(255, 255, 255, 0.8)";
        ctx.fillText(
          `${fish.pathType} | spd:${Math.round(fish.speed || 0)} | dur:${Math.round((fish.lifespan || 0) / 1000)}s`,
          currentX,
          currentY + 18,
        );
        // --- END DEBUG RENDER ---
      });

      // Draw Local Gun and All Other Idle Guns
      const mPos = mousePosRef.current;
      const sIdx = seatIndexRef.current;
      const fixedOrigins =
        gameDataSeatsRef.current.length > 0
          ? gameDataSeatsRef.current
          : [
              { x: 480, y: 1080 },
              { x: 1440, y: 1080 },
              { x: 480, y: 0 },
              { x: 1440, y: 0 },
            ];

      const drawGun = (
        origin: any,
        target: any,
        status: "local" | "opponent" | "empty",
        label?: string,
      ) => {
        const dx = target.x - origin.x;
        const dy = target.y - origin.y;
        const angle = Math.atan2(dy, dx);

        // Base Circle
        ctx.beginPath();
        ctx.arc(origin.x, origin.y, 30, 0, 2 * Math.PI);
        if (status === "local") {
          ctx.fillStyle = "rgba(255, 50, 100, 0.9)";
        } else if (status === "opponent") {
          ctx.fillStyle = "rgba(100, 150, 255, 0.8)";
        } else {
          ctx.fillStyle = "rgba(100, 100, 100, 0.4)";
        }
        ctx.fill();

        // Barrel
        ctx.save();
        ctx.translate(origin.x, origin.y);
        ctx.rotate(angle);
        ctx.beginPath();
        ctx.rect(0, -10, 50, 20); // Length 50, width 20
        ctx.fillStyle =
          status === "empty" ? "rgba(150, 150, 150, 0.5)" : "#ddd";
        ctx.fill();
        ctx.restore();

        // Laser line
        if (status === "local") {
          ctx.beginPath();
          ctx.moveTo(
            origin.x + Math.cos(angle) * 50,
            origin.y + Math.sin(angle) * 50,
          );
          ctx.lineTo(target.x, target.y);
          ctx.strokeStyle = "rgba(255, 50, 100, 0.6)";
          ctx.lineWidth = 2;
          ctx.setLineDash([5, 5]);
          ctx.stroke();
          ctx.setLineDash([]);
        }

        // Label
        if (label) {
          ctx.fillStyle =
            status === "local"
              ? "#ff3366"
              : status === "opponent"
                ? "rgba(100, 150, 255, 0.8)"
                : "rgba(150, 150, 150, 0.5)";
          ctx.font =
            status === "empty" ? "12px sans-serif" : "bold 14px sans-serif";
          ctx.textAlign = "center";
          const yOffset = origin.y > 540 ? -45 : 55;
          ctx.fillText(label, origin.x, origin.y + yOffset);
        }
      };

      fixedOrigins.forEach((origin, idx) => {
        if (idx === sIdx) {
          drawGun(origin, mPos, "local", "You");
        } else {
          let occupiedBy = "";
          for (const [uid, seat] of Object.entries(roomSeatsRef.current)) {
            if (seat === idx) {
              occupiedBy = uid;
              break;
            }
          }

          const dx = 960 - origin.x;
          const dy = 540 - origin.y;
          const len = Math.sqrt(dx * dx + dy * dy);

          if (occupiedBy) {
            const aimTarget = opponentTargetsRef.current[occupiedBy] || {
              x: 960,
              y: 540,
            };
            drawGun(origin, aimTarget, "opponent", occupiedBy.substring(0, 4));
          } else {
            const target = {
              x: origin.x + (dx / len) * 100,
              y: origin.y + (dy / len) * 100,
            };
            drawGun(origin, target, "empty", "Empty");
          }
        }
      });

      // Draw and Update Bouncing Bullets
      const bulletsToKeep: any[] = [];
      const currentSocket = socketRef.current;
      const currentRoomId = roomIdRef.current;
      const currentWeapon = weaponRef.current;

      activeBulletsRef.current.forEach((bullet) => {
        // 1. Move Bullet
        bullet.x += bullet.vx * dt;
        bullet.y += bullet.vy * dt;

        // 2. Wall Bounce (1920x1080)
        if (bullet.x <= 0) {
          bullet.x = 0;
          bullet.vx *= -1;
          bullet.bounces = (bullet.bounces || 0) + 1;
        } else if (bullet.x >= 1920) {
          bullet.x = 1920;
          bullet.vx *= -1;
          bullet.bounces = (bullet.bounces || 0) + 1;
        }
        if (bullet.y <= 0) {
          bullet.y = 0;
          bullet.vy *= -1;
          bullet.bounces = (bullet.bounces || 0) + 1;
        } else if (bullet.y >= 1080) {
          bullet.y = 1080;
          bullet.vy *= -1;
          bullet.bounces = (bullet.bounces || 0) + 1;
        }

        // Bullet Expiry: If bounced too many times, send a MISS SHOOT so balance is deducted
        if ((bullet.bounces || 0) > currentMaxBounces) {
          // Only local bullets should send SHOOT — opponent bullets silently expire
          if (bullet.owner === "local" && currentSocket && currentRoomId) {
            const missPayload = {
              type: "SHOOT",
              payload: {
                weaponType: currentWeapon,
                origin: bullet.origin,
                aimTarget: {
                  x: Math.round(bullet.x),
                  y: Math.round(bullet.y),
                  z: 0,
                },
                clientTs: Date.now() + clockOffset,
                roomId: currentRoomId,
                bulletId: bullet.id,
                bounceCount: bullet.bounces,
              },
            };
            addLog(
              "OUT",
              `${FishEvents.CLIENT.REQUEST} (SHOOT-MISS)`,
              missPayload,
            );
            currentSocket.emit(
              FishEvents.CLIENT.REQUEST,
              JSON.stringify(missPayload),
              (ack: any) => {
                const parsed = typeof ack === "string" ? JSON.parse(ack) : ack;
                addLog("IN", "ACK (SHOOT-MISS)", parsed);
                // Update balance — server deducts weapon cost even on miss
                const newBal = parsed?.payload?.balance ?? parsed?.balance;
                if (newBal !== undefined) setBalance(newBal);
              },
            );
          }
          return; // Remove from bulletsToKeep regardless
        }

        // 3. Collision Detection
        let hitFish = null;
        for (const fish of activeFishRef.current) {
          const spawnTime = fish?.spawnTime || now;
          const lifespan = fish?.lifespan || 10000;
          const elapsed = now - spawnTime;
          if (now < spawnTime) continue;

          // Same keyframe logic as render loop — ensures hit detection matches visuals
          let progress = elapsed / lifespan;

          if (fish.motionKeyframes && fish.motionKeyframes.length > 0) {
            const dist = getDistanceFromKeyframes(
              elapsed,
              fish.motionKeyframes,
            );
            const totalLength =
              fish.pathParams.cumulativeLengths[
                fish.pathParams.cumulativeLengths.length - 1
              ];
            progress = totalLength > 0 ? dist / totalLength : 0;
          }

          const points = fish.pathParams?.points || [];
          if (points.length > 0) {
            const currentPos = getSplinePositionAt(
              points,
              fish.pathParams.cumulativeLengths,
              progress,
            );
            const dist = Math.hypot(
              bullet.x - currentPos.x,
              bullet.y - currentPos.y,
            );
            const radius = fish.hitboxRadius || 40;
            if (dist <= radius) {
              hitFish = fish;
              break;
            }
          }
        }

        if (hitFish) {
          // Bullet Hit! Emit SHOOT with precise hit coordinates
          if (currentSocket && currentRoomId && bullet.owner === "local") {
            const payload = {
              type: "SHOOT",
              payload: {
                weaponType: currentWeapon,
                origin: bullet.origin,
                aimTarget: {
                  x: Math.round(bullet.x),
                  y: Math.round(bullet.y),
                  z: 0,
                },
                clientTs: Date.now() + clockOffset,
                roomId: currentRoomId,
                bulletId: bullet.id,
                ...(bullet.bounces > 0 ? { bounceCount: bullet.bounces } : {}),
              },
            };
            addLog("OUT", `${FishEvents.CLIENT.REQUEST} (SHOOT-HIT)`, payload);
            currentSocket.emit(
              FishEvents.CLIENT.REQUEST,
              JSON.stringify(payload),
              (ack: any) => {
                const parsed = typeof ack === "string" ? JSON.parse(ack) : ack;
                addLog("IN", "ACK (SHOOT)", parsed);
                if (parsed?.payload?.balance !== undefined) {
                  // Use a functional state update or an event to avoid stale closures,
                  // but for dummy client this is okay since setBalance is stable.
                  // It's better to rely on server balance sync event, but let's just log it.
                }
              },
            );
          }

          // Register Hit Effect for Animation
          hitEffectsRef.current.push({
            x: bullet.x,
            y: bullet.y,
            timestamp: now,
            duration: 400, // 400ms explosion
          });
        } else {
          bulletsToKeep.push(bullet);

          // Draw Bullet
          ctx.save();
          ctx.translate(bullet.x, bullet.y);
          ctx.rotate(Math.atan2(bullet.vy, bullet.vx));

          ctx.beginPath();
          ctx.arc(0, 0, 6, 0, 2 * Math.PI);
          ctx.fillStyle = bullet.color;
          ctx.fill();
          ctx.strokeStyle = "white";
          ctx.lineWidth = 1;
          ctx.stroke();

          // Bullet Trail
          ctx.beginPath();
          ctx.moveTo(-6, 0);
          ctx.lineTo(-20, 0);
          ctx.strokeStyle = bullet.color;
          ctx.lineWidth = 4;
          ctx.lineCap = "round";
          ctx.stroke();

          ctx.restore();
        }
      });
      activeBulletsRef.current = bulletsToKeep;

      // Draw Hit Effects
      const activeHits: any[] = [];
      hitEffectsRef.current.forEach((effect) => {
        const elapsed = now - effect.timestamp;
        if (elapsed < effect.duration) {
          activeHits.push(effect);
          const progress = elapsed / effect.duration;

          // Expand radius and fade out
          const radius = 10 + progress * 30;
          const opacity = 1 - progress;

          ctx.beginPath();
          ctx.arc(effect.x, effect.y, radius, 0, 2 * Math.PI);
          ctx.fillStyle = `rgba(255, 200, 0, ${opacity * 0.8})`;
          ctx.fill();
          ctx.strokeStyle = `rgba(255, 255, 255, ${opacity})`;
          ctx.lineWidth = 2;
          ctx.stroke();
        }
      });
      hitEffectsRef.current = activeHits;

      // --- SPLASH EFFECTS ---
      const activeSplash: any[] = [];
      splashEffectsRef.current.forEach((effect) => {
        const age = now - effect.timestamp;
        if (age > 600) return; // Lasts 600ms
        activeSplash.push(effect);

        const targetPos = currentFishPositions[effect.targetFishId];
        if (!targetPos) return;

        // Draw lightning/laser from origin to target
        ctx.beginPath();
        ctx.moveTo(effect.origin.x, effect.origin.y);
        // Add a slight jaggedness for "lightning" effect
        const midX =
          (effect.origin.x + targetPos.x) / 2 + (Math.random() - 0.5) * 50;
        const midY =
          (effect.origin.y + targetPos.y) / 2 + (Math.random() - 0.5) * 50;
        ctx.lineTo(midX, midY);
        ctx.lineTo(targetPos.x, targetPos.y);

        ctx.strokeStyle =
          effect.outcome === SHOT_OUTCOME.KILL
            ? "rgba(255, 50, 50, 0.8)"
            : "rgba(100, 200, 255, 0.6)";
        ctx.lineWidth = effect.outcome === SHOT_OUTCOME.KILL ? 4 : 2;
        ctx.stroke();

        // If it's a kill, draw a coin effect at the end of the duration
        if (effect.outcome === SHOT_OUTCOME.KILL && age > 400 && age < 600) {
          ctx.fillStyle = "gold";
          ctx.beginPath();
          ctx.arc(
            targetPos.x,
            targetPos.y - (age - 400) * 0.5,
            15,
            0,
            Math.PI * 2,
          );
          ctx.fill();
          ctx.strokeStyle = "#b8860b";
          ctx.lineWidth = 2;
          ctx.stroke();
          ctx.fillStyle = "#fff";
          ctx.font = "bold 14px Arial";
          ctx.textAlign = "center";
          ctx.fillText("$", targetPos.x, targetPos.y - (age - 400) * 0.5 + 5);
        }
      });
      splashEffectsRef.current = activeSplash;

      // Draw Visual Shots (Opponents & Server Feedback)
      const currentVisualShots: any[] = [];
      visualShotsRef.current.forEach((shot) => {
        const elapsed = now - shot.timestamp;
        if (elapsed < 500) {
          // Show for 500ms
          currentVisualShots.push(shot);
          const progress = elapsed / 500;
          const opacity = 1 - progress;

          // Draw a line from origin to aimTarget
          ctx.save();
          ctx.beginPath();
          ctx.moveTo(shot.origin.x, shot.origin.y);
          ctx.lineTo(shot.aimTarget.x, shot.aimTarget.y);
          ctx.strokeStyle = shot.color;
          ctx.globalAlpha = opacity;
          ctx.lineWidth = 2;
          ctx.setLineDash([10, 10]);
          ctx.stroke();
          ctx.setLineDash([]);

          // Draw impact circle at aimTarget
          ctx.beginPath();
          ctx.arc(
            shot.aimTarget.x,
            shot.aimTarget.y,
            30 * progress,
            0,
            2 * Math.PI,
          );
          ctx.fillStyle = shot.color;
          ctx.globalAlpha = opacity * 0.5;
          ctx.fill();

          // Draw username near impact
          ctx.globalAlpha = opacity;
          ctx.fillStyle = shot.color;
          ctx.font = "12px sans-serif";
          ctx.textAlign = "center";
          ctx.fillText(shot.username, shot.aimTarget.x, shot.aimTarget.y - 15);

          ctx.restore();
        }
      });
      visualShotsRef.current = currentVisualShots;
      // Draw crosshair
      ctx.beginPath();
      ctx.arc(mPos.x, mPos.y, 12, 0, 2 * Math.PI);
      ctx.strokeStyle = "rgba(255, 50, 100, 0.9)";
      ctx.lineWidth = 3;
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(mPos.x - 20, mPos.y);
      ctx.lineTo(mPos.x + 20, mPos.y);
      ctx.moveTo(mPos.x, mPos.y - 20);
      ctx.lineTo(mPos.x, mPos.y + 20);
      ctx.stroke();

      animationFrameId = requestAnimationFrame(render);
    };

    render();
    return () => cancelAnimationFrame(animationFrameId);
  }, []); // Empty dependency array ensures loop runs smoothly without being recreated

  const handleCanvasClick = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!socket || !roomId) return;
    const canvas = canvasRef.current;
    if (!canvas) return;

    const rect = canvas.getBoundingClientRect();
    const scaleX = 1920 / rect.width;
    const scaleY = 1080 / rect.height;

    const clickX = (e.clientX - rect.left) * scaleX;
    const clickY = (e.clientY - rect.top) * scaleY;

    const currentClientTs = Date.now() + clockOffset;
    const fixedOrigins =
      gameDataSeats.length > 0
        ? gameDataSeats
        : [
            { x: 480, y: 1080, z: 0 },
            { x: 1440, y: 1080, z: 0 },
            { x: 480, y: 0, z: 0 },
            { x: 1440, y: 0, z: 0 },
          ];
    const originPos =
      seatIndex >= 0 && seatIndex < fixedOrigins.length
        ? fixedOrigins[seatIndex]
        : { x: 960, y: 1080, z: 0 };
    const targetPos = { x: Math.round(clickX), y: Math.round(clickY), z: 0 };

    // --- STEP 1: Send FIRE event (visual broadcast to opponents) ---
    const dx0 = targetPos.x - originPos.x;
    const dy0 = targetPos.y - originPos.y;
    const angle = (Math.atan2(dy0, dx0) * 180) / Math.PI;
    const bulletId = `bullet_${Math.random().toString(36).substring(2, 9)}`;
    const firePayload = {
      type: "FIRE",
      payload: {
        weaponType: weapon,
        origin: originPos,
        angle,
        clientTs: currentClientTs,
        roomId,
        bulletId,
      },
    };
    addLog("OUT", `${FishEvents.CLIENT.REQUEST} (FIRE)`, firePayload);
    socket.emit(
      FishEvents.CLIENT.REQUEST,
      JSON.stringify(firePayload),
      () => {},
    );

    // --- STEP 2: Spawn local bouncing bullet (SHOOT sent automatically when it hits a fish) ---
    const speed = 1200; // pixels per second
    const dx = dx0;
    const dy = dy0;
    const distance = Math.hypot(dx, dy);

    if (distance > 0) {
      activeBulletsRef.current.push({
        id: bulletId,
        x: originPos.x,
        y: originPos.y,
        vx: speed * Math.cos((angle * Math.PI) / 180),
        vy: speed * Math.sin((angle * Math.PI) / 180),
        owner: "local",
        origin: originPos,
        color: "#ff3366",
        bounces: 0,
      });
    }
  };

  const handleCanvasMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const scaleX = 1920 / rect.width;
    const scaleY = 1080 / rect.height;

    setMousePos({
      x: (e.clientX - rect.left) * scaleX,
      y: (e.clientY - rect.top) * scaleY,
    });
  };

  const handleHome = () => {
    if (!socket) return;
    const payload = { type: "HOME", payload: { roomId } };
    addLog("OUT", `${FishEvents.CLIENT.REQUEST} (HOME)`, payload);
    socket.emit(
      FishEvents.CLIENT.REQUEST,
      JSON.stringify(payload),
      (ack: any) => {},
    );
  };

  const handleExit = () => {
    navigate("/");
  };

  return (
    <div className="fixed inset-0 z-[100] flex flex-col w-screen h-screen bg-[#050e1d] text-white font-mono text-sm overflow-hidden select-none">
      <div className="flex-none bg-[#0a192f] px-4 py-3 border-b border-blue-900/40 flex justify-between items-center shadow-lg">
        <div>
          <h1 className="text-xl font-bold text-cyan-400">🐟 ML-FG Game</h1>
          <div className="text-blue-300/70 text-xs mt-1">
            Server Time:{" "}
            <span className="text-pink-400 mr-4">
              {new Date(serverTime).toLocaleTimeString("en-US", {
                hour12: false,
                hour: "2-digit",
                minute: "2-digit",
                second: "2-digit",
                fractionalSecondDigits: 3,
              } as any)}
            </span>
            Status:{" "}
            <span
              className={
                socket?.connected
                  ? "text-green-400 font-bold"
                  : "text-red-400 font-bold"
              }
            >
              {socket?.connected ? "Connected" : "Disconnected"}
            </span>
            <span className="mx-2 text-blue-800">|</span>
            Room: <span className="text-yellow-400">{roomId}</span>
          </div>
        </div>

        <div className="flex gap-4 items-center">
          <div className="bg-[#0f2547] border border-blue-800/50 px-4 py-1.5 rounded-lg shadow-inner text-base">
            <span className="text-blue-300 text-xs uppercase tracking-wider mr-2">
              Balance:
            </span>
            <span className="font-bold text-emerald-400">
              ${balance !== null ? balance.toFixed(2) : "0.00"}
            </span>
          </div>
          <button
            onClick={handleExit}
            className="bg-red-600/80 hover:bg-red-600 px-4 py-1.5 rounded-lg font-bold text-xs uppercase tracking-wider transition-colors shadow-md border border-red-500/50"
          >
            Exit
          </button>
        </div>
      </div>

      <div className="flex flex-1 overflow-hidden lg:flex-row flex-col bg-[#050e1d]">
        {/* Game Canvas Area */}
        <div className="flex-[2] bg-[#050e1d] border-r border-blue-900/40 relative flex flex-col p-4 justify-center items-center">
          <div className="w-full max-w-6xl aspect-video relative rounded-xl border border-cyan-500/30 overflow-hidden shadow-[0_0_60px_rgba(0,140,255,0.18)] bg-gradient-to-b from-[#091a35] via-[#061327] to-[#040c1e]">
            <canvas
              ref={canvasRef}
              width={1920}
              height={1080}
              className="w-full h-full cursor-crosshair block"
              onClick={handleCanvasClick}
              onMouseMove={handleCanvasMouseMove}
            />
            <div className="absolute top-4 left-4 pointer-events-none bg-black/70 backdrop-blur-sm px-3 py-2 rounded-lg border border-blue-900/50 text-xs text-blue-200 shadow-md">
              <div className="font-bold text-cyan-300 mb-1 flex items-center gap-1.5">
                <span>🎮</span> Visual Testing Canvas
              </div>
              <div>
                • Active Fish:{" "}
                <span className="text-white font-semibold">
                  {activeFish.length}
                </span>
              </div>
              <div>• Click on screen to shoot</div>
              <div>
                • Origin Seat:{" "}
                <span className="text-yellow-400 font-semibold">
                  {seatIndex >= 0 ? seatIndex : "None"}
                </span>
              </div>
              <div>
                • Target: ({Math.round(mousePos.x)}, {Math.round(mousePos.y)})
              </div>
            </div>

            {tideWarningText && (
              <div className="absolute inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm pointer-events-none transition-all duration-300">
                <div className="text-5xl md:text-7xl font-black text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 via-blue-500 to-purple-600 animate-pulse uppercase tracking-widest drop-shadow-[0_0_20px_rgba(0,255,255,0.8)]">
                  {tideWarningText}
                </div>
              </div>
            )}
          </div>
        </div>

        <div className="flex-1 flex flex-col max-w-md bg-[#071326] border-l border-blue-900/40">
          {/* Logs */}
          <div className="flex-1 flex flex-col border-b border-blue-900/40 min-h-0">
            <div className="flex justify-between items-center p-2.5 bg-[#0a1b36] border-b border-blue-900/40 text-xs text-blue-300 shrink-0">
              <span className="font-semibold text-cyan-400">
                Network Traffic
              </span>
              <div className="flex gap-4">
                <label className="flex items-center gap-1 cursor-pointer hover:text-white">
                  <input
                    type="checkbox"
                    checked={showFishLogs}
                    onChange={(e) => setShowFishLogs(e.target.checked)}
                    className="accent-cyan-500"
                  />
                  Spawns
                </label>
                <label className="flex items-center gap-1 cursor-pointer hover:text-white">
                  <input
                    type="checkbox"
                    checked={autoScroll}
                    onChange={(e) => setAutoScroll(e.target.checked)}
                    className="accent-cyan-500"
                  />
                  Scroll
                </label>
              </div>
            </div>

            <div
              ref={logsContainerRef}
              className="flex-1 overflow-y-auto p-3 space-y-2 bg-[#050e1d]/70"
            >
              {logs
                .filter(
                  (log) =>
                    showFishLogs ||
                    (log.event !== FishEvents.SERVER.FISH_SPAWNED &&
                      log.event !== FishEvents.SERVER.FISH_DESPAWNED),
                )
                .map((log) => {
                  const isOut = log.type === "OUT";
                  const isErr = log.type === "ERROR";
                  const isInfo = log.type === "INFO";
                  return (
                    <div
                      key={log.id}
                      className={`p-2 rounded border-l-4 ${
                        isOut
                          ? "bg-blue-950/40 border-cyan-500"
                          : isErr
                            ? "bg-red-950/40 border-red-500"
                            : isInfo
                              ? "bg-slate-900/60 border-slate-500"
                              : "bg-emerald-950/40 border-emerald-500"
                      }`}
                    >
                      <div className="flex justify-between text-xs mb-1 opacity-70">
                        <span className="font-bold">
                          {log.type} - {log.event}
                        </span>
                        <span>{log.time}</span>
                      </div>
                      {log.payload && (
                        <div className="relative group mt-1">
                          <button
                            onClick={() =>
                              navigator.clipboard.writeText(
                                JSON.stringify(log.payload, null, 2),
                              )
                            }
                            className="absolute top-1 right-1 bg-gray-700 hover:bg-gray-600 text-white text-[10px] px-2 py-1 rounded opacity-0 group-hover:opacity-100 transition-opacity"
                          >
                            Copy
                          </button>
                          <pre className="text-[10px] overflow-x-auto whitespace-pre-wrap break-words bg-black/60 p-2 rounded">
                            {JSON.stringify(log.payload, null, 2)}
                          </pre>
                        </div>
                      )}
                    </div>
                  );
                })}
              {/* Logs end */}
            </div>
          </div>

          {/* Controls */}
          <div className="bg-[#0a192f] p-4 shrink-0 border-t border-blue-900/40 shadow-[0_-10px_20px_rgba(0,0,0,0.5)] z-10">
            <div className="flex items-center gap-3">
              <div className="flex-1">
                <label className="block text-xs text-blue-300/80 mb-1">
                  Weapon Type
                </label>
                <select
                  value={weapon}
                  onChange={(e) => setWeapon(e.target.value)}
                  className="w-full bg-[#050e1d] border border-blue-800/60 p-2 rounded text-white focus:border-cyan-400 outline-none"
                >
                  <option value="normal">Normal (Cost: 1)</option>
                </select>
              </div>
              <button
                onClick={handleHome}
                disabled={!socket?.connected}
                className="bg-blue-900/50 hover:bg-blue-800 disabled:opacity-50 border border-blue-700/50 px-3 py-2 rounded text-xs transition-colors self-end h-[38px] text-blue-200"
              >
                Go Home
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default FishGameDummy;
