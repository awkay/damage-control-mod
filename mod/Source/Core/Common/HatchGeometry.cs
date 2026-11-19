using System;

namespace DamageControl.Core.Common
{
	/// <summary>
	/// Vertical extent of a hatch opening in world space (02 conventions): <see cref="LowY"/> is the
	/// opening's lowest point, <see cref="HighY"/> = LowY + collider height x vertical scale.
	/// Built by the Game layer's BoatLayout from the hatch's BoxCollider; replaces every hard-coded
	/// 2022.1 interior height (C3, C12).
	/// </summary>
	public struct HatchGeometry
	{
		public readonly float LowY;
		public readonly float HighY;

		public HatchGeometry(float lowY, float highY)
		{
			LowY = lowY;
			HighY = highY;
		}

		/// <summary>Opening height in metres.</summary>
		public float Height
		{
			get { return HighY - LowY; }
		}

		/// <summary>Finite and with a positive height.</summary>
		public bool IsValid
		{
			get { return DcMath.IsFinite(LowY) && DcMath.IsFinite(HighY) && HighY - LowY > DcMath.Epsilon; }
		}

		/// <summary>
		/// From a box collider: the transform's world Y, the collider's local centre Y and size Y, and the
		/// transform's vertical scale (lossy scale). A negative scale flips the box, so its magnitude is used.
		/// Rotation is ignored: hatches stand upright or lie flat, and only height along world Y matters.
		/// </summary>
		public static HatchGeometry FromBoxCollider(float transformY, float centerY, float sizeY, float scaleY)
		{
			float scale = Math.Abs(scaleY);
			float height = Math.Abs(sizeY) * scale;
			float centre = transformY + centerY * scaleY;
			float low = centre - height * 0.5f;
			return new HatchGeometry(low, low + height);
		}

		/// <summary>
		/// The 02 convention exactly: lowY is the hatch's lowest-point marker (the sill the game's own water
		/// transfer uses) and highY = lowY + collider height x vertical scale. Preferred over
		/// <see cref="FromBoxCollider"/> when the hatch has that marker.
		/// </summary>
		public static HatchGeometry FromLowestPoint(float lowestPointY, float sizeY, float scaleY)
		{
			return new HatchGeometry(lowestPointY, lowestPointY + Math.Abs(sizeY * scaleY));
		}

		public override string ToString()
		{
			return "HatchGeometry(lowY=" + LowY + ", highY=" + HighY + ")";
		}
	}
}
