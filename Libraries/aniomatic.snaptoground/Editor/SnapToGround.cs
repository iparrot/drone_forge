using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Snap To Ground
///
/// Drops each selected GameObject straight down until one of its lowest
/// readable mesh points reaches the first surface below it.
/// Rotation, X, and Y are never touched.
///
/// If no readable render mesh is available, the tool falls back to the
/// object's world bounds so non-model objects still work.
///
/// Shortcut : END
/// Menu     : Editor -> Workbench -> Snap To Ground
/// </summary>
public static class SnapToGround
{
	private const float TraceDistance = 100000.0f;
	private const float BottomPointTolerance = 0.5f;
	private const float SampleTraceLift = 0.5f;
	private const float GroundEpsilon = 0.01f;

	private sealed class SnapData
	{
		public float BottomWorldZ;
		public float PivotToBottom;
		public List<Vector3> BottomSamplePoints = new();
	}

	[Menu( "Editor", "Workbench/Snap To Ground" )]
	[Shortcut( "workbench.snap-to-ground", "END" )]
	public static void Execute()
	{
		using var scope = SceneEditorSession.Scope();

		if ( EditorScene.Selection.Count == 0 )
			return;

		var gos = EditorScene.Selection.OfType<GameObject>().ToArray();
		if ( gos.Length == 0 )
			return;

		var snapData = gos.ToDictionary( go => go, BuildSnapData );

		gos.DispatchPreEdited( nameof( GameObject.LocalPosition ) );

		var sorted = gos
			.OrderBy( go => snapData[go].BottomWorldZ )
			.ToArray();

		using ( SceneEditorSession.Active
			.UndoScope( "Snap Object(s) To Ground" )
			.WithGameObjectChanges( gos, GameObjectUndoFlags.Properties )
			.Push() )
		{
			var pending = new HashSet<GameObject>( sorted );

			foreach ( var go in sorted )
			{
				pending.Remove( go );
				TrySnapToGround( go, pending, snapData[go] );
			}
		}

		gos.DispatchEdited( nameof( GameObject.LocalPosition ) );
	}

	private static SnapData BuildSnapData( GameObject go )
	{
		var bounds = go.GetBounds();
		var data = new SnapData
		{
			BottomWorldZ = bounds.Mins.z,
			PivotToBottom = go.WorldPosition.z - bounds.Mins.z
		};

		if ( TryCollectBottomSamplePoints( go, out var bottomSamplePoints, out var meshBottomZ ) )
		{
			data.BottomWorldZ = meshBottomZ;
			data.PivotToBottom = go.WorldPosition.z - meshBottomZ;
			data.BottomSamplePoints = bottomSamplePoints;
		}

		return data;
	}

	private static void TrySnapToGround( GameObject go, HashSet<GameObject> pending, SnapData data )
	{
		if ( data.BottomSamplePoints.Count > 0 &&
			TryGetBestDropDistance( go, pending, data.BottomSamplePoints, out var dropDistance ) )
		{
			if ( dropDistance > 0.0f )
			{
				go.WorldPosition = new Vector3(
					go.WorldPosition.x,
					go.WorldPosition.y,
					go.WorldPosition.z - dropDistance
				);
			}

			return;
		}

		if ( !TryTraceGround(
			go,
			pending,
			go.WorldPosition,
			go.WorldPosition + Vector3.Down * TraceDistance,
			out var hitPosition ) )
		{
			return;
		}

		go.WorldPosition = new Vector3(
			go.WorldPosition.x,
			go.WorldPosition.y,
			hitPosition.z + data.PivotToBottom
		);
	}

	private static bool TryGetBestDropDistance(
		GameObject go,
		HashSet<GameObject> pending,
		IReadOnlyList<Vector3> samplePoints,
		out float dropDistance )
	{
		dropDistance = float.PositiveInfinity;

		foreach ( var samplePoint in samplePoints )
		{
			if ( !TryTraceGround(
				go,
				pending,
				samplePoint + Vector3.Up * SampleTraceLift,
				samplePoint + Vector3.Down * TraceDistance,
				out var hitPosition ) )
			{
				continue;
			}

			if ( hitPosition.z > samplePoint.z + GroundEpsilon )
				continue;

			var candidateDrop = MathF.Max( 0.0f, samplePoint.z - hitPosition.z );
			if ( candidateDrop < dropDistance )
				dropDistance = candidateDrop;
		}

		if ( float.IsPositiveInfinity( dropDistance ) )
			return false;

		if ( dropDistance <= GroundEpsilon )
			dropDistance = 0.0f;

		return true;
	}

	private static bool TryCollectBottomSamplePoints(
		GameObject go,
		out List<Vector3> samplePoints,
		out float bottomWorldZ )
	{
		samplePoints = new List<Vector3>();
		bottomWorldZ = float.PositiveInfinity;
		var allVertices = new List<Vector3>();

		foreach ( var renderer in EnumerateModelRenderers( go ) )
		{
			if ( !renderer.IsValid() || !renderer.Model.IsValid() || !renderer.Model.HasRenderMeshes() )
				continue;

			var vertices = renderer.Model.GetVertices();
			if ( vertices is null || vertices.Length == 0 )
				continue;

			var transform = renderer.GameObject.WorldTransform;

			foreach ( var vertex in vertices )
			{
				var worldPoint = transform.PointToWorld( vertex.Position );
				allVertices.Add( worldPoint );
				bottomWorldZ = MathF.Min( bottomWorldZ, worldPoint.z );
			}
		}

		if ( allVertices.Count == 0 || float.IsPositiveInfinity( bottomWorldZ ) )
			return false;

		var threshold = bottomWorldZ + BottomPointTolerance;
		var uniquePoints = new HashSet<Vector3>();

		foreach ( var worldPoint in allVertices )
		{
			if ( worldPoint.z > threshold )
				continue;

			if ( uniquePoints.Add( worldPoint ) )
				samplePoints.Add( worldPoint );
		}

		return samplePoints.Count > 0;
	}

	private static bool TryTraceGround(
		GameObject go,
		HashSet<GameObject> pending,
		Vector3 start,
		Vector3 end,
		out Vector3 hitPosition )
	{
		var traceBuilder = SceneEditorSession.Active.Scene.Trace
			.Ray( start, end )
			.IgnoreGameObjectHierarchy( go )
			.WithoutTags( "trigger" )
			.UseRenderMeshes( true )
			.UsePhysicsWorld( true );

		foreach ( var unsnapped in pending )
			traceBuilder = traceBuilder.IgnoreGameObjectHierarchy( unsnapped );

		var trace = traceBuilder.Run();
		if ( !trace.Hit )
		{
			hitPosition = default;
			return false;
		}

		hitPosition = trace.HitPosition;
		return true;
	}

	private static IEnumerable<ModelRenderer> EnumerateModelRenderers( GameObject root )
	{
		if ( !root.IsValid() )
			yield break;

		foreach ( var renderer in root.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelf ) )
		{
			if ( renderer.IsValid() )
				yield return renderer;
		}

		foreach ( var child in root.Children )
		{
			if ( !child.IsValid() )
				continue;

			foreach ( var renderer in EnumerateModelRenderers( child ) )
				yield return renderer;
		}
	}
}
