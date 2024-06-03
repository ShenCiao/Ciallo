#pragma once

#include <treehh/tree.hh>

template<class Archive>
void save(Archive& archive, const tree<entt::entity>& t)
{
	// DFS save
	for (auto it = t.begin(), end = t.end(); it != end; ++it)
	{
		archive(*it, it.number_of_children());
	}
}

template<class Archive>
void static DFSLoadTree(tree<entt::entity>::iterator parentIt, unsigned numChildren, Archive& archive, tree<entt::entity>& t)
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
		DFSLoadTree(childIt, numGrandChildren, archive, t);
	}
}

template<class Archive>
void load(Archive& archive, tree<entt::entity>& t)
{
	entt::entity head;
	unsigned int numChildren;
	archive(head, numChildren); // head is null entity
	DFSLoadTree(t.begin(), numChildren, archive, t);
}
