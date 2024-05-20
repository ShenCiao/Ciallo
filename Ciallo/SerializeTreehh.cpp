#include "pch.hpp"
#include "SerializeTreehh.h"

template<class Archive>
void recursivelyLoadTree(tree<entt::entity>::iterator parentIt, unsigned numChildren, Archive& archive, tree<entt::entity>& t)
{
	if (numChildren == 0)
	{
		return;
	}
	for (unsigned i = 0; i < numChildren; ++i)
	{
		entt::entity childE;
		unsigned numGrandChildren;
		archive(childE, numGrandChildren);
		auto childIt = t.append_child(parentIt, childE);
		recursivelyLoadTree(childIt, numGrandChildren, archive, t);
	}
}
