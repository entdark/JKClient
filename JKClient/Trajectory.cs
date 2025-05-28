using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct Trajectory {
		public TrajectoryType Type;
		public int Time;
		public int Duration;
		public Vector3 Base;
		public Vector3 Delta;
		public Vector3 Evaluate(int time) {
			float deltaTime;
			switch (this.Type) {
			case TrajectoryType.Stationary:
			case TrajectoryType.Interpolate:
				return this.Base;
			case TrajectoryType.Linear:
				deltaTime = (time - this.Time) * 0.001f;
				return this.Base + this.Delta * deltaTime;
			case TrajectoryType.Sine:
				deltaTime = (time - this.Time) / (float)this.Duration;
				float phase = (float)Math.Sin(deltaTime * Math.PI * 2.0);
				return this.Base + this.Delta * phase;
			case TrajectoryType.LinearStop:
				if (time > this.Time + this.Duration) {
					time = this.Time + this.Duration;
				}
				deltaTime = (time - this.Time) * 0.001f;
				if (deltaTime < 0.0f) {
					deltaTime = 0.0f;
				}
				return this.Base + this.Delta * deltaTime;
			case TrajectoryType.NonLinearStop:
				if (time > this.Time + this.Duration) {
					time = this.Time + this.Duration;
				}
				if (time - this.Time > this.Duration || time - this.Time <= 0) {
					deltaTime = 0;
				} else {
					deltaTime = this.Duration*0.001f*(float)Math.Cos((90.0f-(90.0f*(time-this.Time)/(float)this.Duration))*Math.PI/180.0f);
				}
				return this.Base + this.Delta * deltaTime;
			case TrajectoryType.Gravity:
				deltaTime = (time - this.Time) * 0.001f;
				var result = this.Base + this.Delta * deltaTime;
				result.Z -= 0.5f * ClientGame.DefaultGravity * deltaTime * deltaTime;
				return result;
			default:
				throw new JKClientException($"EvaluateTrajectory: unknown type: {this.Type}");
			}
		}
	}
	public enum TrajectoryType : int {
		Stationary,
		Interpolate,
		Linear,
		LinearStop,
		NonLinearStop,
		Sine,
		Gravity
	}
}