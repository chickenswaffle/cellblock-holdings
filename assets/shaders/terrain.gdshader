shader_type spatial;
render_mode diffuse_lambert, specular_disabled;

// One texel per grid tile; linear filtering gives free soft blending
// across tile boundaries instead of a hard grid pattern.
uniform sampler2D splat_tex : source_color, filter_linear, repeat_disable;
uniform float noise_scale = 60.0;
uniform float noise_strength = 0.06;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

void fragment() {
	vec3 base = texture(splat_tex, UV).rgb;
	float n = hash(floor(UV * noise_scale)) - 0.5;
	ALBEDO = clamp(base + vec3(n * noise_strength), 0.0, 1.0);
	ROUGHNESS = 0.92;
}
