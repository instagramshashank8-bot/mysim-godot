using System;
using System.Collections.Generic;
namespace MySimulator.Rebuild {
public sealed class RaceSeries {
 public readonly int[] Tracks;public readonly int Car,Laps;public int Round {get;private set;}
 public readonly List<int> Places=new List<int>();public bool Complete=>Round>=Tracks.Length;public int CurrentTrack=>!Complete?Tracks[Round]:throw new InvalidOperationException("Series finished");
 public RaceSeries(int[] tracks,int car,int laps){if(tracks==null||tracks.Length==0||tracks.Length>60)throw new ArgumentException("Select1to60rounds");foreach(int track in tracks)if(track<0||track>=SourceRoutes.Points.Length)throw new ArgumentOutOfRangeException(nameof(tracks));if(car<0||car>=CarCatalog.Count||laps<1||laps>20)throw new ArgumentOutOfRangeException("Car/laps");Tracks=(int[])tracks.Clone();Car=car;Laps=laps;}
 public void FinishRound(int place){if(Complete||place<1)throw new InvalidOperationException("Invalid series result");Places.Add(place);Round++;}
}
}
