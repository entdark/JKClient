using System;
using System.Runtime.InteropServices;

namespace JKClient {
	internal unsafe sealed class Huffman : IDisposable {
		private const int HuffMax = 256;
		private const int NodesMax = Huffman.HuffMax*3;
		private const int NotYetTransmitted = Huffman.HuffMax;
		private const int InternalNode = Huffman.HuffMax+1;
		private int bloc = 0;
		private int blocNode = 0,
					blocPtrs = 0;
		private Node* tree, lhead, ltail;
		private Node** freelist;
		private Node** loc;
		private Node* nodeList;
		private Node** nodePtrs;
		public Huffman(bool decompressor = false) {
			this.loc = (Node**)Marshal.AllocHGlobal(sizeof(Node*)*(Huffman.HuffMax+1));
			this.nodeList = (Node*)Marshal.AllocHGlobal(sizeof(Node)*Huffman.NodesMax);
			this.nodePtrs = (Node**)Marshal.AllocHGlobal(sizeof(Node*)*Huffman.NodesMax);
			Common.MemSet(this.loc, 0, sizeof(Node*)*(Huffman.HuffMax+1));
			Common.MemSet(this.nodeList, 0, sizeof(Node)*Huffman.NodesMax);
			Common.MemSet(this.nodePtrs, 0, sizeof(Node*)*Huffman.NodesMax);
			this.tree = this.lhead = this.loc[Huffman.NotYetTransmitted] = &(this.nodeList[this.blocNode++]);
			if (decompressor) {
				this.ltail = this.tree;
			}
			this.tree->symbol = Huffman.NotYetTransmitted;
			this.tree->weight = 0;
			this.lhead->next = this.lhead->prev = null;
			this.tree->parent = this.tree->left = this.tree->right = null;
			if (!decompressor) {
				this.loc[Huffman.NotYetTransmitted] = this.tree;
			}
		}
		private void Transmit(int ch, byte []fout) {
			if (this.loc[ch] == null) {
				this.Transmit(Huffman.NotYetTransmitted, fout);
				for (int i = 7; i >= 0; i--) {
					this.AddBit((sbyte)((ch >> i) & 0x1), fout);
				}
			} else {
				this.Send(this.loc[ch], null, fout);
			}
		}
		public void AddReference(byte ch) {
			if (this.loc[ch] == null) {
				Node *tnode = &(this.nodeList[this.blocNode++]);
				Node *tnode2 = &(this.nodeList[this.blocNode++]);
				tnode2->symbol = Huffman.InternalNode;
				tnode2->weight = 1;
				tnode2->next = this.lhead->next;
				if (this.lhead->next != null) {
					this.lhead->next->prev = tnode2;
					if (this.lhead->next->weight == 1) {
						tnode2->head = this.lhead->next->head;
					} else {
						tnode2->head = this.GetPPNode();
						*tnode2->head = tnode2;
					}
				} else {
					tnode2->head = this.GetPPNode();
					*tnode2->head = tnode2;
				}
				this.lhead->next = tnode2;
				tnode2->prev = this.lhead;
				tnode->symbol = ch;
				tnode->weight = 1;
				tnode->next = this.lhead->next;
				if (this.lhead->next != null) {
					this.lhead->next->prev = tnode;
					if (this.lhead->next->weight == 1) {
						tnode->head = this.lhead->next->head;
					} else {
						tnode->head = this.GetPPNode();
						*tnode->head = tnode2;
					}
				} else {
					tnode->head = this.GetPPNode();
					*tnode->head = tnode;
				}
				this.lhead->next = tnode;
				tnode->prev = this.lhead;
				tnode->left = tnode->right = null;
				if (this.lhead->parent != null) {
					if (this.lhead->parent->left == this.lhead) {
						this.lhead->parent->left = tnode2;
					} else {
						this.lhead->parent->right = tnode2;
					}
				} else {
					this.tree = tnode2;
				}
				tnode2->right = tnode;
				tnode2->left = this.lhead;
				tnode2->parent = this.lhead->parent;
				this.lhead->parent = tnode->parent = tnode2;
				this.loc[ch] = tnode;
				this.Increment(tnode2->parent);
			} else {
				this.Increment(this.loc[ch]);
			}
		}
		private void Increment(Node *node) {
			Node *lnode;
			if (node == null) {
				return;
			}
			if (node->next != null && node->next->weight == node->weight) {
				lnode = *node->head;
				if (lnode != node->parent) {
					this.Swap(lnode, node);
				}
				Huffman.Swaplist(lnode, node);
			}
			if (node->prev != null && node->prev->weight == node->weight) {
				*node->head = node->prev;
			} else {
				*node->head = null;
				this.FreePPNode(node->head);
			}
			node->weight++;
			if (node->next != null && node->next->weight == node->weight) {
				node->head = node->next->head;
			} else {
				node->head = this.GetPPNode();
				*node->head = node;
			}
			if (node->parent != null) {
				this.Increment(node->parent);
				if (node->prev == node->parent) {
					Huffman.Swaplist(node, node->parent);
					if (*node->head == node) {
						*node->head = node->parent;
					}
				}
			}
		}
		private void Swap(Node *node1, Node *node2) {
			Node* par1, par2;
			par1 = node1->parent;
			par2 = node2->parent;
			if (par1 != null) {
				if (par1->left == node1) {
					par1->left = node2;
				} else {
					par1->right = node2;
				}
			} else {
				this.tree = node2;
			}
			if (par2 != null) {
				if (par2->left == node2) {
					par2->left = node1;
				} else {
					par2->right = node1;
				}
			} else {
				this.tree = node1;
			}
			node1->parent = par2;
			node2->parent = par1;
		}
		private static void Swaplist(Node *node1, Node *node2) {
			Node *par1;
			par1 = node1->next;
			node1->next = node2->next;
			node2->next = par1;
			par1 = node1->prev;
			node1->prev = node2->prev;
			node2->prev = par1;
			if (node1->next == node1) {
				node1->next = node2;
			}
			if (node2->next == node2) {
				node2->next = node1;
			}
			if (node1->next != null) {
				node1->next->prev = node1;
			}
			if (node2->next != null) {
				node2->next->prev = node2;
			}
			if (node1->prev != null) {
				node1->prev->next = node1;
			}
			if (node2->prev != null) {
				node2->prev->next = node2;
			}
		}
		private Node **GetPPNode() {
			if (this.freelist == null) {
				var n = &(this.nodePtrs[this.blocPtrs++]);
				return n;
			} else {
				Node **tppnode = this.freelist;
				this.freelist = (Node**)*tppnode;
				return tppnode;
			}
		}
		private void FreePPNode(Node **ppnode) {
			*ppnode = (Node*)this.freelist;
			this.freelist = ppnode;
		}
		private void AddBit(sbyte bit, byte []fout) {
			if ((this.bloc&7) == 0) {
				fout[(this.bloc>>3)] = 0;
			}
			fout[(this.bloc>>3)] |= (byte)(bit << (this.bloc&7));
			this.bloc++;
		}
		private int GetBit(byte []fin) {
			int t = (fin[(this.bloc>>3)] >> (this.bloc&7)) & 0x1;
			this.bloc++;
			return t;
		}
		private void Send(Node *node, Node *child, byte []fout) {
			if (node->parent != null) {
				this.Send(node->parent, node, fout);
			}
			if (child != null) {
				if (node->right == child) {
					this.AddBit(1, fout);
				} else {
					this.AddBit(0, fout);
				}
			}
		}
		public void OffsetReceive(ref int ch, byte []fin, ref int offset) {
			Node *node = this.tree;
			this.bloc = offset;
			while (node != null && node->symbol == Huffman.InternalNode) {
				if (this.GetBit(fin) != 0) {
					node = node->right;
				} else {
					node = node->left;
				}
			}
			if (node == null) {
				ch = 0;
				return;
			}
			ch = node->symbol;
			offset = this.bloc;
		}
		public void OffsetTransmit(int ch, byte []fout, ref int offset) {
			this.bloc = offset;
			this.Send(this.loc[ch], null, fout);
			offset = this.bloc;
		}
		public int GetBit(byte []fin, ref int offset) {
			this.bloc = offset;
			int t = (fin[(this.bloc>>3)] >> (this.bloc&7)) & 0x1;
			this.bloc++;
			offset = this.bloc;
			return t;
		}
		public void PutBit(int bit, byte []fout, ref int offset) {
			this.bloc = offset;
			if ((this.bloc&7) == 0) {
				fout[(this.bloc>>3)] = 0;
			}
			fout[(this.bloc>>3)] |= (byte)(bit << (this.bloc&7));
			this.bloc++;
			offset = this.bloc;
		}
		public static unsafe void Compress(Message msg, int offset) {
			int ch;
			byte []seq = new byte[65536];
			int size = msg.CurSize - offset;
			if (size <= 0) {
				return;
			}
			fixed (byte *b = msg.Data) {
				byte *buffer = b+ + offset;
				using (var huff = new Huffman()) {
					seq[0] = (byte)(size>>8);
					seq[1] = (byte)(size&0xff);
					huff.bloc = 16;
					for (int i = 0; i < size; i++) {
						ch = buffer[i];
						huff.Transmit(ch, seq);
						huff.AddReference((byte)ch);
					}
					huff.bloc += 8;
					msg.CurSize = (huff.bloc>>3) + offset;
					Array.Copy(seq, 0, msg.Data, offset, (huff.bloc>>3));
				}
			}
		}
		public void Dispose() {
			Marshal.FreeHGlobal((IntPtr)this.loc);
			Marshal.FreeHGlobal((IntPtr)this.nodeList);
			Marshal.FreeHGlobal((IntPtr)this.nodePtrs);
		}
		private unsafe struct Node {
			public Node* left, right, parent;
			public Node* next, prev;
			public Node** head;
			public int weight;
			public int symbol;
		}
	}
}
