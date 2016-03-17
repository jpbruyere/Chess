﻿using System;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using GameLib;

namespace Chess
{
	public class ChessShader : Shader
	{
		public ChessShader ()
		{
			vertSource = @"
			#version 330
			precision highp float;

			layout (location = 0) in vec3 in_position;
			layout (location = 1) in vec2 in_tex;
			layout (location = 2) in vec3 in_normal;			

			layout (std140, index = 0) uniform block_data{
				mat4 Projection;
				mat4 ModelView;
				mat4 Normal;
				vec4 lightPos;
				vec4 Color;
			};

			out vec2 texCoord;			
			out vec3 n;			
			out vec4 vEyeSpacePos;
			

			void main(void)
			{				
				texCoord = in_tex;
				n = vec3(Normal * vec4(in_normal, 0));

				vec3 pos = in_position.xyz;

				vEyeSpacePos = ModelView * vec4(pos, 1);
				
				gl_Position = Projection * ModelView * vec4(pos, 1);
			}";

			fragSource = @"
			#version 330			

			precision highp float;

			uniform sampler2D tex;			

			layout (std140, index = 0) uniform block_data{
				mat4 Projection;
				mat4 ModelView;
				mat4 Normal;
				vec4 lightPos;
				vec4 Color;
			};

			in vec2 texCoord;			
			in vec4 vEyeSpacePos;
			in vec3 n;			
			
			out vec4 out_frag_color;

			void main(void)
			{
				vec4 diffTex = texture( tex, texCoord) * Color;
				if (diffTex.a < 0.5)
					discard;

				vec3 l;
				if (lightPos.w == 0.0)
					l = normalize(-lightPos.xyz);
				else
					l = normalize(lightPos.xyz - vEyeSpacePos.xyz);				

				float Idiff = clamp(max(dot(n,l), 0.0),0.5,1.0);

				out_frag_color = vec4(diffTex.rgb*Idiff, diffTex.a);
			}";
			Compile ();
		}

		public int DiffuseTexture;

		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 2, "in_normal");
		}
		protected override void GetUniformLocations ()
		{	
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex(pgmId, "block_data"), 0);
		}	
		public override void Enable ()
		{
			GL.UseProgram (pgmId);
			GL.ActiveTexture (TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, DiffuseTexture);
		}
	}
}
