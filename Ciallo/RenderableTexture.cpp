#include "pch.hpp"
#include "RenderableTexture.h"

RenderableTexture::RenderableTexture(int width, int height, int multiSample): Width(width),
                                                                              Height(height),
                                                                              MultiSample(multiSample)
{
	GenBuffers();
}

RenderableTexture::RenderableTexture(RenderableTexture&& other) noexcept
{
	*this = std::move(other);
}

RenderableTexture& RenderableTexture::operator=(RenderableTexture&& other) noexcept
{
	if (this == &other)
		return *this;
	Width = other.Width;
	Height = other.Height;
	MultiSample = other.MultiSample;
	MSFramebuffer = other.MSFramebuffer;
	MSColorTexture = other.MSColorTexture;
	MSDepthStencilTexture = other.MSDepthStencilTexture;
	Framebuffer = other.Framebuffer;
	ColorTexture = other.ColorTexture;
	DepthStencilTexture = other.DepthStencilTexture;

	other.ZeroizeBuffers();
	return *this;
}

RenderableTexture::~RenderableTexture()
{
	DelBuffers();
}

void RenderableTexture::GenBuffers()
{
	auto setTextureParameter = [](GLuint tex)
	{
		glTextureParameteri(tex, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
		glTextureParameteri(tex, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
		glTextureParameteri(tex, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
		glTextureParameteri(tex, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
	};

	glCreateTextures(GL_TEXTURE_2D, 1, &ColorTexture);
	setTextureParameter(ColorTexture);
	glTextureStorage2D(ColorTexture, 1, GL_RGBA8, Width, Height);

	glCreateTextures(GL_TEXTURE_2D, 1, &DepthStencilTexture);
	setTextureParameter(DepthStencilTexture);
	glTextureStorage2D(DepthStencilTexture, 1, GL_DEPTH24_STENCIL8, Width, Height);

	glCreateFramebuffers(1, &Framebuffer);
	glNamedFramebufferTexture(Framebuffer, GL_COLOR_ATTACHMENT0, ColorTexture, 0);
	glNamedFramebufferTexture(Framebuffer, GL_DEPTH_STENCIL_ATTACHMENT, DepthStencilTexture, 0);

	if (glCheckNamedFramebufferStatus(Framebuffer, GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
	{
		throw std::runtime_error("Framebuffer incomplete!");
	}

	if(!MultiSample)
	{
		MSFramebuffer = Framebuffer;
		MSColorTexture = ColorTexture;
		MSDepthStencilTexture = DepthStencilTexture;
		return;
	}

	glCreateTextures(GL_TEXTURE_2D_MULTISAMPLE, 1, &MSColorTexture);
	setTextureParameter(MSColorTexture);
	glTextureStorage2DMultisample(MSColorTexture, 1, GL_RGBA8, Width, Height, false);

	glCreateTextures(GL_TEXTURE_2D_MULTISAMPLE, 1, &MSDepthStencilTexture);
	setTextureParameter(MSDepthStencilTexture);
	glTextureStorage2DMultisample(MSDepthStencilTexture, 1, GL_RGBA8, Width, Height, false);

	glCreateFramebuffers(1, &MSFramebuffer);
	glNamedFramebufferTexture(MSFramebuffer, GL_COLOR_ATTACHMENT0, MSColorTexture, 0);
	glNamedFramebufferTexture(MSFramebuffer, GL_DEPTH_STENCIL_ATTACHMENT, MSDepthStencilTexture, 0);

	if (glCheckNamedFramebufferStatus(MSFramebuffer, GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
	{
		throw std::runtime_error("MSFramebuffer incomplete!");
	}
}

void RenderableTexture::DelBuffers()
{
	glDeleteTextures(1, &ColorTexture);
	glDeleteTextures(1, &DepthStencilTexture);
	glDeleteFramebuffers(1, &Framebuffer);

	glDeleteTextures(1, &MSColorTexture);
	glDeleteTextures(1, &MSDepthStencilTexture);
	glDeleteFramebuffers(1, &MSFramebuffer);
	ZeroizeBuffers();
}

// Warning: changing bound framebuffer
void RenderableTexture::CopyMS()
{
	if(MultiSample == 0)
	{
		return;
	}
	glBindFramebuffer(GL_READ_FRAMEBUFFER, MSFramebuffer);
	glBindFramebuffer(GL_DRAW_FRAMEBUFFER, Framebuffer);
	glBlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, GL_COLOR_BUFFER_BIT, GL_NEAREST);
}

void RenderableTexture::BindFramebuffer()
{
	glBindFramebuffer(GL_FRAMEBUFFER, MSFramebuffer);
	glViewport(0, 0, Width, Height);
}

void RenderableTexture::ZeroizeBuffers()
{
	ColorTexture = 0;
	DepthStencilTexture = 0;
	Framebuffer = 0;
	MSColorTexture = 0;
	MSDepthStencilTexture = 0;
	MSFramebuffer = 0;
}
