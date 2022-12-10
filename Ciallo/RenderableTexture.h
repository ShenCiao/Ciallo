#pragma once

// RAII object
class RenderableTexture
{
public:
	int Width, Height;
	int MultiSample = 0;

	GLuint MSFramebuffer = 0;
	GLuint MSColorTexture = 0;
	GLuint MSDepthStencilTexture = 0;
	GLuint Framebuffer = 0;
	GLuint ColorTexture = 0;
	GLuint DepthStencilTexture = 0;

	RenderableTexture() = default;
	RenderableTexture(int width, int height, int multiSample = 0);
	RenderableTexture(const RenderableTexture& other) = delete;
	RenderableTexture(RenderableTexture&& other) noexcept;
	RenderableTexture& operator=(const RenderableTexture& other) = delete;
	RenderableTexture& operator=(RenderableTexture&& other) noexcept;
	~RenderableTexture();

	
	void GenBuffers();
	void DelBuffers();
	void CopyMS();
	void BindFramebuffer();
private:
	void ZeroizeBuffers();
};

