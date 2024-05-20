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
void static recursivelyLoadTree(tree<entt::entity>::iterator parentIt, unsigned numChildren, Archive& archive, tree<entt::entity>& t);

template<class Archive>
void load(Archive& archive, tree<entt::entity>& t)
{
	entt::entity head;
	unsigned int numChildren;
	archive(head, numChildren); // head is null entity
	auto root = t.set_head(head);
	recursivelyLoadTree(root, numChildren, archive);
}
