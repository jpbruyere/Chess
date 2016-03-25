using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Chess
{
	public class ReflexionShader : Tetra.Shader
	{
		#region CTOR
		public ReflexionShader ():base()
		{
		}
		#endregion
		public override void Init ()
		{
			fragSource = @"
			#version 330
			precision lowp float;

			uniform sampler2D tex;

			in vec2 texCoord;
			out vec4 out_frag_color;

			void main(void)
			{
				vec4 c = texture( tex, texCoord);
				if (c.a == 0.0)
					discard;
				out_frag_color = vec4(texture( tex, texCoord).rgb, 0.3);
			}";			
			base.Init ();
		}		
	}
}