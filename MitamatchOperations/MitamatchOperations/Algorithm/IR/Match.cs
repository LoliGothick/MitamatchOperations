﻿using System;
using System.Drawing;
using OpenCvSharp.Extensions;
using OpenCvSharp;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using mitama.Domain;
using Windows.Storage;

namespace mitama.Algorithm.IR;

internal class Match
{
    public static async Task<(Bitmap, Memoria[])> Recognise(Bitmap img)
    {
        var target = img.ToMat();
        var grayMat = target.CvtColor(ColorConversionCodes.BGR2GRAY);
        var thresholdMat = grayMat.Threshold(230, 255, ThresholdTypes.BinaryInv);
        thresholdMat.FindContours(out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

        var rects = new List<Rect>();
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            switch (area)
            {
                case <= 10000 or > 100000:
                    continue;
                default:
                    {
                        var rect = Cv2.BoundingRect(contour);
                        if (IsSquare(rect))
                        {
                            rects.Add(rect);
                        }
                        break;
                    }
            }
        }
        var akaze = AKAZE.Create();
        Costume? detectedCostume;
        {
            var costume = rects.MinBy(memoria => memoria.Top)!;
            // remove costume
            rects.Remove(costume);

            var source = new Mat();
            akaze.DetectAndCompute(target.Clone(costume), null, out _, source);

            var templates = await Task.WhenAll(Costume.List.Select(async c =>
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(c.Uri);
                var image = new Bitmap((await FileIO.ReadBufferAsync(file)).AsStream());
                var descriptors = new Mat();
                akaze.DetectAndCompute(image.ToMat(), null, out _, descriptors);
                return (costume: c, descriptors);
            }));

            detectedCostume = templates.MinBy(template =>
            {
                var (_, train) = template;
                var matcher = new BFMatcher(NormTypes.Hamming);
                var matches = matcher.Match(source, train);
                var sum = matches.Sum(x => x.Distance);
                return sum / matches.Length;
            }).costume;
        }

        rects = Interpolation(rects, img.Width);

        foreach (var rect in rects) Cv2.Rectangle(target, rect, Scalar.Aquamarine, 5);

        {
            var templates = await Task.WhenAll(Memoria.List.Where(detectedCostume.Value.CanBeEquipped).Select(async memoria =>
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(memoria.Uri);
                var image = new Bitmap((await FileIO.ReadBufferAsync(file)).AsStream());
                var descriptors = new Mat();
                akaze.DetectAndCompute(image.ToMat(), null, out _, descriptors);
                return (memoria, descriptors);
            }));

            var detected = rects.AsParallel()
                .Select(target.Clone)
                .Select(mat =>
                {
                    var descriptors = new Mat();
                    akaze.DetectAndCompute(mat, null, out _, descriptors);
                    return descriptors;
                }).Select(source =>
                {
                    return templates.MinBy(template =>
                    {
                        var (_, train) = template;
                        var matcher = new BFMatcher(NormTypes.Hamming);
                        var matches = matcher.Match(source, train);
                        var sum = matches.Sum(x => x.Distance);
                        return sum / matches.Length;
                    }).memoria;
                }).ToArray();
            return (target.ToBitmap(), detected);
        }
    }

    private static bool IsSquare(Rect rect)
        => Math.Min(Math.Abs(rect.Height), Math.Abs(rect.Width)) > 0.95 * Math.Max(Math.Abs(rect.Height), Math.Abs(rect.Width));

    private static List<Rect> Interpolation(ICollection<Rect> memorias, int width)
    {
        var size = (int)memorias.Select(memoria => (memoria.Width + memoria.Height) / 2.0).Mean();

        List<List<Rect>> lines = new();
        while (memorias.Count != 0)
        {
            var top = memorias.MinBy(memoria => memoria.Top)!;
            var line = new List<Rect> { top };
            memorias.Remove(top);
            foreach (var memoria in memorias.Where(memoria => Math.Abs(memoria.Top - top.Top) < 10).ToArray())
            {
                line.Add(memoria);
                memorias.Remove(memoria);
            }
            line.Sort((x, y) => x.Left.CompareTo(y.Left));
            lines.Add(line);
        }

        var margin = lines[0].Zip(lines[0].Skip(1)).Select(a => a.Second.Left - a.First.Right).Min();

        foreach (var line in lines)
        {
            foreach (var (a, b) in line.Zip(line.Skip(1)).ToArray())
            {
                var space = b.Left - a.Right;
                if (space < a.Width) continue;

                var num = space / (margin + a.Width);
                var bottomLeft = a.BottomRight;

                for (var i = 0; i < num; i++)
                {
                    bottomLeft = bottomLeft with
                    {
                        X = bottomLeft.X + margin
                    };
                    var topRight = bottomLeft with
                    {
                        X = bottomLeft.X + a.Width,
                        Y = bottomLeft.Y - a.Width
                    };
                    line.Add(Cv2.BoundingRect(new[] { topRight, bottomLeft }));
                }
            }
            line.Sort((x, y) => x.Left.CompareTo(y.Left));
        }

        for (var i = 0; i < 2; i++)
        {
            while (lines[i].Count < 10)
            {
                var line = lines[i];
                var left = line.First();
                var right = line.Last();

                if (left.Left > width - right.Right)
                {
                    line.Insert(0, Cv2.BoundingRect(new[]
                    {
                        left.TopLeft with { X = left.TopLeft.X - margin },
                        left.TopLeft with { X = left.TopLeft.X - margin - size, Y = left.TopLeft.Y + size },
                    }));
                }
                else
                {
                    line.Add(Cv2.BoundingRect(new[]
                    {
                        right.BottomRight with { X = right.BottomRight.X + margin },
                        right.BottomRight with { X = right.BottomRight.X + margin + size, Y = right.BottomRight.Y - size },
                    }));
                }
            }
        }

        return lines.SelectMany(xs => xs).ToList();
    }
}