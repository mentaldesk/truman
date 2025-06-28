<script lang="ts">
    import Header from '$lib/components/Header.svelte';
    import ValueCard from '$lib/components/ValueCard.svelte';
    import { valuesStore } from '$lib/stores/values';
    
    let draggedValue: any = null;
    let dragOverIndex: number | null = null;
    let isDraggingOver = false;

    function handleDragStart(value: any) {
        draggedValue = value;
    }
    
    function handleDragEnd() {
        draggedValue = null;
        dragOverIndex = null;
        isDraggingOver = false;
    }
    
    function handleDragOver(e: DragEvent, index: number | null = null) {
        e.preventDefault();
        isDraggingOver = true;
        dragOverIndex = index;
    }

    function handleDragLeave() {
        isDraggingOver = false;
    }
    
    function handleDrop(e: DragEvent, index: number | null = null) {
        e.preventDefault();
        if (!draggedValue) return;

        // If we're dropping into the selected list
        if ($valuesStore.selected.length === 0) {
            // First item being added
            valuesStore.selectValue(draggedValue);
        } else if (index !== null) {
            // Reordering in selected list
            const currentIndex = $valuesStore.selected.findIndex(v => v.id === draggedValue.id);
            if (currentIndex !== -1) {
                valuesStore.reorderSelected(currentIndex, index);
            } else {
                // Adding new item at specific position
                valuesStore.selectValue(draggedValue);
                // TODO: Add support for inserting at specific position
            }
        } else {
            // Adding to end of selected list
            valuesStore.selectValue(draggedValue);
        }
        
        draggedValue = null;
        dragOverIndex = null;
        isDraggingOver = false;
    }
</script>

<div class="h-full flex flex-col">
    <Header />
    
    <main class="flex-1 max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8 flex flex-col">
        <!-- Purpose section -->
        <div class="flex items-center gap-8 mb-8 flex-shrink-0">
            <div class="flex-shrink-0 bg-gray-50 rounded-full p-4 flex items-center justify-center overflow-hidden">
                <img src="/icons/perspective-large.png" alt="Perspective" class="w-24 h-24 object-cover rounded-full" />
            </div>
            <div class="flex-1">
                <p class="text-base text-gray-600">
                    Your reality is defined by the sum of all the little things you choose to pay attention to. 
                </p>
                <p class="mt-4 text-base text-gray-500">
                    Consciously choosing <strong>the media</strong> you consume and <strong>the sentiment</strong> 
                    it conveys allows you to take control of your reality and focus on the things that are important
                    to you. 
                </p>
            </div>
        </div>

        <!-- Values selection grid -->
        <div class="grid grid-cols-2 gap-8 flex-1 min-h-0">
            <!-- Selected Values (Left Panel) -->
            <div 
                class="bg-white rounded-lg p-6 flex flex-col overflow-hidden relative {isDraggingOver ? 'ring-2 ring-blue-500 ring-opacity-50' : ''}"
                on:dragover={(e) => handleDragOver(e)}
                on:dragleave={handleDragLeave}
                on:drop={(e) => handleDrop(e)}
            >
                <div class="flex-shrink-0 mb-6">
                    <h2 class="text-xl font-semibold text-gray-900 mb-4">Your Values</h2>
                    <p class="text-sm text-gray-500">Drag and arrange your most important values in order of priority.</p>
                </div>
                
                <div class="overflow-y-auto min-h-0 flex-1">
                    <div class="space-y-3">
                        {#each $valuesStore.selected as value, index}
                            <div
                                class="relative"
                                on:dragover={(e) => handleDragOver(e, index)}
                                on:dragleave={handleDragLeave}
                                on:drop={(e) => handleDrop(e, index)}
                            >
                                {#if dragOverIndex === index}
                                    <div class="absolute inset-0 border-2 border-blue-500 rounded-lg -m-1"></div>
                                {/if}
                                <div
                                    on:dragstart={() => handleDragStart(value)}
                                    on:dragend={handleDragEnd}
                                >
                                    <ValueCard {value} />
                                </div>
                            </div>
                        {/each}
                        
                        {#if $valuesStore.selected.length === 0}
                            <div class="text-center py-8 text-gray-400 border-2 border-dashed rounded-lg">
                                Drag values here to start building your perspective
                            </div>
                        {/if}
                    </div>
                </div>
            </div>

            <!-- Available Values (Right Panel) -->
            <div class="bg-white rounded-lg p-6 flex flex-col overflow-hidden">
                <div class="flex-shrink-0 mb-6">
                    <h2 class="text-xl font-semibold text-gray-900 mb-4">Available Values</h2>
                    <p class="text-sm text-gray-500">Choose from these values to define your perspective.</p>
                </div>
                
                <div class="overflow-y-auto min-h-0 flex-1">
                    {#each Array.from(new Set($valuesStore.available.map(v => v.category.name))) as categoryName}
                        <div class="mb-6 last:mb-0">
                            <h3 class="font-medium text-gray-700 mb-3">{categoryName}</h3>
                            <div class="space-y-2">
                                {#each $valuesStore.available.filter(v => v.category.name === categoryName) as value}
                                    <div
                                        on:dragstart={() => handleDragStart(value)}
                                        on:dragend={handleDragEnd}
                                    >
                                        <ValueCard {value} />
                                    </div>
                                {/each}
                            </div>
                        </div>
                    {/each}
                </div>
            </div>
        </div>
    </main>
</div> 