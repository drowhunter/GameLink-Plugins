"""Builds a synthetic Star Citizen event stream, runs SC-Motion-Haptic-Bridge's lm_simhub4 bridge logic
(v3 Path + Seat, v4 Haptics) over it at 100 Hz exactly like lm_simhub4.run(), and writes the events and
the expected per-tick outputs for the C# parity test."""
import json, math, sys
scripts, out = sys.argv[1], sys.argv[2]
sys.path.insert(0, scripts)
import lm_simhub3 as v3
import lm_simhub4 as v4

evs = []
def post(t, ev, fields):
    evs.append({"t": round(t, 6), "ev": ev, "m": list(fields.keys()), "v": list(fields.values())})

FR = 1 / 60.0
T0 = 1.0
post(T0, 1, {1: 0.0})
t = T0
end = T0 + 9.0
role_next = T0
while t < end:
    r = t - T0
    if r < 7.6 and t >= role_next:
        post(t, 10, {38: 1})
        role_next += 0.1
    if 7.6 <= r < 7.7:
        post(t, 2, {})
        post(t, 10, {38: 0})
    if 2.4 <= r < 3.0:          # data gap in flight: stale ramp
        t += FR
        continue
    post(t, 0, {93: FR})
    if r < 1.0:                 # parked, engines on
        gear, v, a, th, w = 1.0, [0, 0, 0], [0, 0, 9.81], [0, 0, 9.81], [0, 0, 0]
    elif r < 1.6:               # lift off and climb
        gear, v, a, th, w = 1.0, [0, 0, 2.0 * (r - 1.0) / 0.6 + 0.2], [0, 0, 12.0], [0, 0, 21.0], [0, 0, 0]
    elif r < 4.0:               # flight: gear up, rotations, boost, thrust changes
        s = r - 1.6
        gear = 0.0
        v = [3 * math.sin(s), 60 + 20 * s, 2 * math.cos(s)]
        a = [4 * math.sin(2 * s), 15 + 10 * math.sin(s), -2 + 3 * math.cos(3 * s)]
        th = [0, 25 + 30 * abs(math.sin(s)), 9.81]
        w = [0.4 * math.sin(1.3 * s), 0.6 * math.sin(0.7 * s), -0.5 * math.cos(s)]
        if 2.0 <= s < 2.05:
            a = [0, 500, 0]     # glitch frame: held
        if 2.0 <= s < 2.6 and int(s * 60) % 6 == 0:
            post(t, 4, {41: 1.0, 43: 0.1})
    elif r < 5.2:               # approach and touchdown with gear down
        s = r - 4.0
        gear = 1.0
        vz = -3.0 if s < 0.6 else 0.0
        v = [0, max(0.0, 5 - 8 * s), vz]
        a = [0, -2, 9.0]
        th = [0, 0, 9.0]
        w = [0, 0, 0]
    else:                       # parked again after the gap
        gear, v, a, th, w = 1.0, [0, 0, 0], [0, 0, 9.81], [0, 0, 9.81], [0, 0, 0]
    f = {4: 1.0, 9: v, 34: [0, 0, 0], 35: w, 38: th, 40: a, 41: [0, 0, 0], 92: gear}
    if 3.0 <= r < 3.02:
        f[34] = [20.0, 0, 0]    # rotational shock
    post(t, 3, f)
    if abs(r - 3.5) < FR / 2:
        post(t, 7, {39: 0.01, 40: 0.0, 42: 0.5, 43: 0.1})
    t += FR

with open(out + "/events.ndjson", "w") as fh:
    for e in evs:
        fh.write(json.dumps(e) + "\n")

# lm_simhub4.run(), without sockets and with a fixed clock
st_d, st_t = {}, 0.0
seat, hp, path = v3.Seat(), v4.Haptics(), v3.Path()
i = 0
rows = []
n = int((end + 0.5 - T0) * v3.RATE)
for k in range(n + 1):
    now = round(T0 + k / v3.RATE, 6)
    while i < len(evs) and evs[i]["t"] <= now:
        p = evs[i]; i += 1
        d = dict(zip(p["m"], p["v"]))
        hp.on_event(p["ev"], d, p["t"])
        if p["ev"] == 3:
            st_d, st_t = d, p["t"]
        elif p["ev"] in (10, 2):
            seat.on_event(p["ev"], d, p["t"])
    seated = seat.seated(now)
    active = st_t > 0 and now - st_t < v3.STALE
    f = path.active(st_d) if active else path.stale()
    f = seat.apply(f, now, 1.0 / v3.RATE)
    g = seat.g
    ex = hp.values(now, seated, g) if active else hp.values(now, False, 0.0)
    rows.append({"now": now, "motion": list(f), "g": g, "active": int(active),
                 "SpeedKmh": ex["SpeedKmh"], "EngineRpm": ex["EngineRpm"], "Throttle": ex["Throttle"],
                 "EngineStarted": ex["EngineStarted"], "FlightIsOnGround": ex["FlightIsOnGround"],
                 "Susp": ex["SuspensionVelocityFrontLeftMps"]})
with open(out + "/expected.ndjson", "w") as fh:
    for r in rows:
        fh.write(json.dumps(r) + "\n")
print(len(evs), "events,", len(rows), "ticks, touchdowns", hp.td_count,
      "max susp", max(r["Susp"] for r in rows), "ground changes",
      sum(1 for a, b in zip(rows, rows[1:]) if a["FlightIsOnGround"] != b["FlightIsOnGround"]),
      "max g", max(r["g"] for r in rows), "max |surge|", max(abs(r["motion"][3]) for r in rows))
