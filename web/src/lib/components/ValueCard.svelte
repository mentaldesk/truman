<script lang="ts">
    export let value: {
        name: string;
        description: string;
        category: {
            emoji: string;
            name: string;
        };
    };
    export let draggable = true;
</script>

<div 
    class="bg-white p-4 rounded-lg shadow-sm border border-gray-200 hover:shadow-md transition-shadow duration-200"
    {draggable}
    on:dragstart
    on:dragend
>
    <div class="flex items-start gap-3">
        <div class="text-2xl flex-shrink-0" aria-hidden="true">
            {value.category.emoji}
        </div>
        <div>
            <h3 class="font-medium text-gray-900">{value.name}</h3>
            <p class="text-sm text-gray-500 mt-1">{value.description}</p>
            <p class="text-xs text-gray-400 mt-2">{value.category.name}</p>
        </div>
    </div>
</div> 