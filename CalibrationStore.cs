using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;

namespace CommunityMinimap;

internal static class CalibrationStore
{
    private const int SupportedFormatVersion = 1;
    private static readonly Dictionary<string, AffineProjection> Projections =
        new(StringComparer.OrdinalIgnoreCase);

    public static bool IsCalibrated(string mapId) =>
        mapId != null && Projections.ContainsKey(mapId);

    public static bool TryWorldToMap(string mapId, Vector3 position, out Vector2 uv)
    {
        uv = default;
        if (mapId == null || !Projections.TryGetValue(mapId, out AffineProjection projection))
            return false;

        uv = projection.Project(position.x, position.z);
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    public static void Load(string path, Action<string> log, Action<string> warn)
    {
        Projections.Clear();
        if (!File.Exists(path))
        {
            File.WriteAllText(path,
                "{\r\n  \"formatVersion\": 1,\r\n  \"maps\": []\r\n}\r\n");
            log($"Created calibration template: {path}");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            CalibrationFile file = JsonSerializer.Deserialize<CalibrationFile>(json, options);
            if (file == null)
                throw new InvalidDataException("The calibration file is empty.");
            if (file.FormatVersion != SupportedFormatVersion)
                throw new InvalidDataException(
                    $"Unsupported calibration format {file.FormatVersion}; expected {SupportedFormatVersion}.");

            foreach (MapCalibration map in file.Maps ?? Array.Empty<MapCalibration>())
            {
                if (string.IsNullOrWhiteSpace(map.MapId))
                {
                    warn("Ignored a calibration entry without mapId.");
                    continue;
                }

                if (!AffineProjection.TryCreate(map, out AffineProjection projection,
                        out string error))
                {
                    warn($"Ignored calibration '{map.MapId}': {error}");
                    continue;
                }

                Projections[map.MapId] = projection;
                log($"Loaded affine calibration: {map.MapId} ({map.Points.Length} points). ");
            }
        }
        catch (Exception ex)
        {
            warn($"Failed loading calibrations.json; built-in calibrations remain available. {ex}");
        }
    }

    private sealed class CalibrationFile
    {
        public int FormatVersion { get; set; }
        public MapCalibration[] Maps { get; set; } = Array.Empty<MapCalibration>();
    }

    private sealed class MapCalibration
    {
        public string MapId { get; set; } = "";
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public CalibrationPoint[] Points { get; set; } = Array.Empty<CalibrationPoint>();
    }

    private sealed class CalibrationPoint
    {
        public double WorldX { get; set; }
        public double WorldZ { get; set; }
        public double MapX { get; set; }
        public double MapY { get; set; }
        public string Label { get; set; } = "";
    }

    private sealed class AffineProjection
    {
        private readonly double[] _u;
        private readonly double[] _v;

        private AffineProjection(double[] u, double[] v)
        {
            _u = u;
            _v = v;
        }

        public Vector2 Project(double worldX, double worldZ) => new(
            (float)(_u[0] * worldX + _u[1] * worldZ + _u[2]),
            (float)(_v[0] * worldX + _v[1] * worldZ + _v[2]));

        public static bool TryCreate(MapCalibration map, out AffineProjection projection,
            out string error)
        {
            projection = null;
            error = "";
            if (map.ImageWidth <= 0 || map.ImageHeight <= 0)
            {
                error = "imageWidth and imageHeight must be positive.";
                return false;
            }
            if (map.Points == null || map.Points.Length < 3)
            {
                error = "at least three control points are required.";
                return false;
            }

            var normal = new double[3, 3];
            var targetU = new double[3];
            var targetV = new double[3];
            foreach (CalibrationPoint point in map.Points)
            {
                if (point.MapX < 0 || point.MapX > map.ImageWidth ||
                    point.MapY < 0 || point.MapY > map.ImageHeight)
                {
                    error = $"point '{point.Label}' lies outside the declared image dimensions.";
                    return false;
                }

                double[] row = { point.WorldX, point.WorldZ, 1d };
                double u = point.MapX / map.ImageWidth;
                double v = 1d - point.MapY / map.ImageHeight;
                for (int i = 0; i < 3; i++)
                {
                    targetU[i] += row[i] * u;
                    targetV[i] += row[i] * v;
                    for (int j = 0; j < 3; j++)
                        normal[i, j] += row[i] * row[j];
                }
            }

            if (!TrySolve3x3(normal, targetU, out double[] solvedU) ||
                !TrySolve3x3(normal, targetV, out double[] solvedV))
            {
                error = "control points are collinear or numerically unstable.";
                return false;
            }

            projection = new AffineProjection(solvedU, solvedV);
            return true;
        }

        private static bool TrySolve3x3(double[,] matrix, double[] target,
            out double[] solution)
        {
            var augmented = new double[3, 4];
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    augmented[row, column] = matrix[row, column];
                augmented[row, 3] = target[row];
            }

            for (int pivot = 0; pivot < 3; pivot++)
            {
                int bestRow = pivot;
                for (int row = pivot + 1; row < 3; row++)
                {
                    if (Math.Abs(augmented[row, pivot]) > Math.Abs(augmented[bestRow, pivot]))
                        bestRow = row;
                }
                if (Math.Abs(augmented[bestRow, pivot]) < 1e-9)
                {
                    solution = null;
                    return false;
                }
                if (bestRow != pivot)
                {
                    for (int column = pivot; column < 4; column++)
                        (augmented[pivot, column], augmented[bestRow, column]) =
                            (augmented[bestRow, column], augmented[pivot, column]);
                }

                double divisor = augmented[pivot, pivot];
                for (int column = pivot; column < 4; column++)
                    augmented[pivot, column] /= divisor;
                for (int row = 0; row < 3; row++)
                {
                    if (row == pivot)
                        continue;
                    double factor = augmented[row, pivot];
                    for (int column = pivot; column < 4; column++)
                        augmented[row, column] -= factor * augmented[pivot, column];
                }
            }

            solution = new[] { augmented[0, 3], augmented[1, 3], augmented[2, 3] };
            return true;
        }
    }
}
